using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Melon.Interface;
using Melon.Models;
using Serilog;

namespace Melon.LocalClasses.Networking;

/// <summary>
/// Keeps Melon findable: the local network broadcast, the router port, the public address and the Pkarr record. Runs every
/// 10 minutes, and right away when settings change.
/// </summary>
public static class RemoteAccessService
{
    private static readonly object Gate = new();
    private static readonly SemaphoreSlim CycleLock = new(1, 1);
    private static TaskCompletionSource kick = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static RemoteAccessStatus status = Off("Off");
    private static IPAddress? publicV4, publicV6;
    private static (int Internal, int External)? mapped;
    private static string? pkarrPublished;
    private static DateTime pkarrPublishedUtc;
    private static string? broadcastKey;
    private static DateTime? lastOutsideUtc, lastOutsideV4Utc, lastOutsideV6Utc;
    private static NetworkDiagnostics? diagnostics;
    private static (string Path, string? Fingerprint)? customFingerprint;

    private static Settings S => StateManager.MelonSettings;
    public static bool Automatic => S.NetworkMode == "Automatic";

    private static RemoteAccessStatus Off(string method) => new(false, method, false, false, false, null, 0, "Off", null);

    public static RemoteAccessStatus Status { get { lock (Gate) return status with { LastOutsideUtc = lastOutsideUtc }; } }

    public static NetworkDiagnostics? LastDiagnostics
    {
        get
        {
            lock (Gate)
            {
                if (diagnostics != null) return diagnostics;
                try { diagnostics = Storage.LoadConfigFile<NetworkDiagnostics>("NetworkDiagnostics", null, out _); } catch (Exception) { }
                return diagnostics;
            }
        }
        set
        {
            lock (Gate) diagnostics = value;
            try { if (value != null) Storage.SaveConfigFile("NetworkDiagnostics", value, null); } catch (Exception) { }
        }
    }

    /// <summary>An app reached Melon from outside. Which way (IPv4 or IPv6) is remembered, so apps can try what works first.</summary>
    public static void NoteOutside(IPAddress from)
    {
        bool v6 = from.AddressFamily == AddressFamily.InterNetworkV6 && !from.IsIPv4MappedToIPv6;
        var now = DateTime.UtcNow;
        lock (Gate)
        {
            lastOutsideUtc = now;
            if (v6) lastOutsideV6Utc = now; else lastOutsideV4Utc = now;
        }
    }

    /// <summary>Runs the next check now instead of waiting.</summary>
    public static void Kick() { lock (Gate) kick.TrySetResult(); }

    public static async Task RunAsync(CancellationToken stopping)
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                Task wake;
                lock (Gate) { if (kick.Task.IsCompleted) kick = new(TaskCreationOptions.RunContinuationsAsynchronously); wake = kick.Task; }
                try { await Cycle(stopping); }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested) { break; }
                catch (Exception ex) { Log.Warning(ex, "Remote Access check failed."); }
                // Task.Delay throws when stopping; WhenAny doesn't, so check the token after it.
                await Task.WhenAny(wake, Task.Delay(TimeSpan.FromMinutes(10), stopping));
            }
        }
        finally
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            lock (Gate) { settle?.Dispose(); settle = null; }
            LocalBroadcast.Stop();
        }
    }

    private static System.Threading.Timer? settle;

    /// <summary>
    /// This computer's addresses changed (new Wi-Fi, a cable plugged in, a new address from the router, waking up). Windows
    /// sends several of these in a row, so wait until it's been quiet for 3 seconds, then broadcast the new addresses, open
    /// the router port on the new network and publish the new public address.
    /// </summary>
    private static void OnNetworkChanged(object? sender, EventArgs e)
    {
        lock (Gate)
        {
            settle ??= new System.Threading.Timer(_ =>
            {
                NetAddresses.Forget();
                PortMapper.Forget();
                Kick();
            });
            settle.Change(TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>One pass: broadcast, router port, addresses, Pkarr. Returns the new status.</summary>
    public static async Task<RemoteAccessStatus> Cycle(CancellationToken token)
    {
        await CycleLock.WaitAsync(token);
        try
        {
            var s = S; var ra = s.RemoteAccess;
            UpdateBroadcast(s);
            if (!Automatic)
            {
                // Switching to a custom setup turns Remote Access off: close the router port and clear the Pkarr record.
                await Release(ra, token);
                bool exposed = Uri.TryCreate(s.ListeningURL.Replace("://*:", "://any:").Replace("://+:", "://any:"), UriKind.Absolute, out var u) && !u.IsLoopback;
                return Set(new RemoteAccessStatus(exposed, "Custom", false, false, false, null, u?.Port ?? 0, "Custom",
                    "Melon uses the listening address and certificate you set up by hand."));
            }
            if (!ra.Enabled)
            {
                await Release(ra, token);
                return Set(Off("Off"));
            }
            int external = ra.ExternalPort is > 0 and <= 65535 ? ra.ExternalPort : s.HttpsPort;
            string? problem = null;
            bool routerPort = false;
            if (ra.Method == "Automatic")
            {
                publicV4 = await Stun.PublicAddress(AddressFamily.InterNetwork, token) ?? publicV4;
                if (publicV4 != null) ServerAdministration.Instance.RecordPublicAddress(publicV4.ToString());
                if (ra.OpenRouterPort)
                {
                    if (mapped is { } old && (old.Internal != s.HttpsPort || old.External != external)) { await PortMapper.Unmap(old.Internal, old.External); mapped = null; }
                    problem = await PortMapper.Map(s.HttpsPort, external, token);
                    routerPort = problem == null;
                    if (!routerPort)
                    {
                        var lan = NetAddresses.LanIPv4().FirstOrDefault();
                        problem += $" To open it by hand, forward TCP port {external} to {(lan != null ? lan + " " : "this computer ")}port {s.HttpsPort} in your router's settings"
                            + (ra.UseIPv6 ? ", or rely on IPv6." : ".");
                    }
                    if (routerPort) mapped = (s.HttpsPort, external);
                }
                else if (mapped is { } old) { await PortMapper.Unmap(old.Internal, old.External); mapped = null; }
                publicV6 = ra.UseIPv6 ? NetAddresses.GlobalIPv6().FirstOrDefault() : null;
                if (ra.UseIPv6 && publicV6 == null) problem ??= "IPv6 is on, but this computer has no public IPv6 address right now.";
            }
            else
            {
                if (mapped is { } old) { await PortMapper.Unmap(old.Internal, old.External); mapped = null; }
                publicV6 = null;
            }
            if (ra.Method == "Tailscale" && NetAddresses.Tailscale() == null) problem = "Tailscale isn't connected on this computer.";
            if (ra.Method == "ReverseProxy" && string.IsNullOrWhiteSpace(ra.PublicUrl)) problem = "No public address is set for the reverse proxy.";

            bool pkarr = false;
            if (ra.PublishPkarr) pkarr = await PublishPkarr(s, token);

            bool reachable = ra.Method != "Automatic" || routerPort || publicV6 != null || !ra.OpenRouterPort && publicV4 != null;
            string state; string? detail;
            DateTime? outside; lock (Gate) outside = lastOutsideUtc;
            if (!reachable) { state = "Problem"; detail = problem ?? "Melon couldn't find a way in from outside."; }
            else if (outside != null) { state = "Working"; detail = problem; }
            else
            {
                state = "Waiting";
                detail = problem ?? "Nobody has connected from outside yet. To check, open Galia or Watermelon away from home, or on mobile data with Wi-Fi off.";
                if (problem == null && publicV6 != null && !routerPort)
                    detail += $" Most routers block incoming IPv6 until you allow it: if phones can't connect, allow TCP port {s.HttpsPort} to this computer in the router's IPv6 firewall.";
            }
            string? address = ra.Method switch
            {
                "Automatic" => publicV4 != null ? NetAddresses.Url("https", publicV4, external)[8..] : publicV6 != null ? NetAddresses.Url("https", publicV6, s.HttpsPort)[8..] : null,
                "Tailscale" => NetAddresses.Tailscale()?.ToString(),
                _ => ra.PublicUrl,
            };
            return Set(new RemoteAccessStatus(true, ra.Method, routerPort, publicV6 != null, pkarr, address, external, state, detail,
                PublicUrl: ra.Method == "ReverseProxy" ? ra.PublicUrl : null));
        }
        finally { CycleLock.Release(); }
    }

    private static RemoteAccessStatus Set(RemoteAccessStatus value) { lock (Gate) status = value; return Status; }

    /// <summary>Takes down what Remote Access set up: the router port, and the Pkarr record (replaced with one that lists no addresses).</summary>
    public static async Task Release(RemoteAccessSettings ra, CancellationToken token)
    {
        if (mapped is { } old) { await PortMapper.Unmap(old.Internal, old.External); mapped = null; }
        if (pkarrPublished != null && pkarrPublished != "")
        {
            try
            {
                var key = NetworkIdentity.PkarrPublicKey;
                await Pkarr.Publish(NetworkIdentity.PkarrKey, Pkarr.Packet(NetworkIdentity.ServerId, null, [], key), Relays(ra), token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { Log.Debug("Couldn't clear the Pkarr record: {Message}", ex.Message); }
            pkarrPublished = "";
        }
    }

    private static IEnumerable<string> Relays(RemoteAccessSettings ra) => ra.PkarrRelays is { Count: > 0 } r ? r : RemoteAccessSettings.DefaultPkarrRelays;

    private static async Task<bool> PublishPkarr(Settings s, CancellationToken token)
    {
        var urls = Endpoints().Where(e => e.Kind is "public" or "public6" or "tailscale" or "custom").Select(e => e.Url).ToArray();
        var key = NetworkIdentity.PkarrPublicKey;
        var signature = string.Join(",", urls) + "|" + Fingerprint();
        if (signature == pkarrPublished && DateTime.UtcNow - pkarrPublishedUtc < TimeSpan.FromHours(1)) return true;
        var accepted = await Pkarr.Publish(NetworkIdentity.PkarrKey, Pkarr.Packet(NetworkIdentity.ServerId, Fingerprint(), urls, key), Relays(s.RemoteAccess), token);
        if (accepted.Length == 0) { Log.Warning("No Pkarr relay accepted Melon's address."); return false; }
        pkarrPublished = signature; pkarrPublishedUtc = DateTime.UtcNow;
        Log.Information("Published {Count} address(es) through Pkarr ({Relays})", urls.Length, string.Join(", ", accepted));
        return true;
    }

    /// <summary>Starts, restarts (when something it advertises changed) or stops the local network broadcast.</summary>
    public static void UpdateBroadcast(Settings s)
    {
        bool setupDone = !File.Exists(Path.Combine(StateManager.melonPath, "Configs", "setup.pending"));
        bool on = s.Broadcast && setupDone;
        int https = Automatic ? s.HttpsPort : Endpoints().Select(e => Uri.TryCreate(e.Url, UriKind.Absolute, out var u) ? u.Port : 0).FirstOrDefault(p => p > 0);
        string name = LocalBroadcast.NameFor(s.BroadcastName);
        var fingerprint = Fingerprint();
        // The home network addresses are part of the key: on a new network the broadcast has to name the new ones.
        var key = on && https > 0 ? $"{name}|{https}|{(Automatic ? s.HttpPort : 0)}|{fingerprint}|{string.Join(",", NetAddresses.LanIPv4().Select(a => a.ToString()))}" : null;
        lock (BroadcastGate)
        {
            if (key == broadcastKey) return;
            broadcastKey = key;
            if (key == null) LocalBroadcast.Stop();
            else LocalBroadcast.Start(name, https, Automatic ? s.HttpPort : 0, NetworkIdentity.ServerId, fingerprint);
        }
    }

    private static readonly object BroadcastGate = new();

    /// <summary>The certificate fingerprint clients should pin: Melon's own in Automatic mode, or the configured certificate's.</summary>
    public static string? Fingerprint()
    {
        if (Automatic) return NetworkIdentity.Fingerprint(NetworkIdentity.Certificate);
        var path = Security.GetSSLConfig()?.PathToCert;
        if (string.IsNullOrEmpty(path)) return null;
        lock (Gate)
        {
            if (customFingerprint is { } c && c.Path == path) return c.Fingerprint;
            string? fp = null;
            try { using var cert = X509CertificateLoader.LoadPkcs12FromFile(path, Security.GetSSLConfig().Password); fp = NetworkIdentity.Fingerprint(cert); }
            catch (Exception) { }
            customFingerprint = (path, fp);
            return fp;
        }
    }

    /// <summary>Every address clients might reach Melon at, home network first.</summary>
    public static ServerEndpoint[] Endpoints()
    {
        var s = S; var list = new List<ServerEndpoint>();
        var host = Environment.MachineName.ToLowerInvariant();
        bool dnsName = Uri.CheckHostName(host) == UriHostNameType.Dns;
        if (Automatic)
        {
            foreach (var ip in NetAddresses.LanIPv4()) list.Add(new(NetAddresses.Url("https", ip, s.HttpsPort), "lan"));
            if (dnsName) list.Add(new($"https://{host}.local:{s.HttpsPort}", "mdns"));
            var ra = s.RemoteAccess;
            if (ra.Enabled)
            {
                if (ra.Method == "Automatic")
                {
                    int external = ra.ExternalPort is > 0 and <= 65535 ? ra.ExternalPort : s.HttpsPort;
                    DateTime? v4Worked, v6Worked;
                    lock (Gate) { v4Worked = lastOutsideV4Utc; v6Worked = lastOutsideV6Utc; }
                    if (publicV4 != null) list.Add(new(NetAddresses.Url("https", publicV4, external), "public", v4Worked));
                    if (ra.UseIPv6 && (publicV6 ?? NetAddresses.GlobalIPv6().FirstOrDefault()) is { } v6) list.Add(new(NetAddresses.Url("https", v6, s.HttpsPort), "public6", v6Worked));
                }
                if (NetAddresses.Tailscale() is { } ts) list.Add(new(NetAddresses.Url("https", ts, s.HttpsPort), "tailscale"));
                if (ra.Method == "ReverseProxy" && Uri.TryCreate(ra.PublicUrl, UriKind.Absolute, out var pu)) list.Add(new(pu.GetLeftPart(UriPartial.Authority), "custom"));
            }
        }
        else
        {
            var raw = s.ListeningURL.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var listen in raw)
            {
                bool wildcard = listen.Contains("://*:") || listen.Contains("://+:") || listen.Contains("://0.0.0.0:") || listen.Contains("://[::]:");
                if (!Uri.TryCreate(wildcard ? listen.Replace("://*:", "://any:").Replace("://+:", "://any:").Replace("://0.0.0.0:", "://any:").Replace("://[::]:", "://any:") : listen,
                    UriKind.Absolute, out var u)) continue;
                if (wildcard)
                {
                    foreach (var ip in NetAddresses.LanIPv4()) list.Add(new(NetAddresses.Url(u.Scheme, ip, u.Port), "lan"));
                    if (dnsName) list.Add(new($"{u.Scheme}://{host}.local:{u.Port}", "mdns"));
                }
                else if (!u.IsLoopback) list.Add(new(u.GetLeftPart(UriPartial.Authority), "custom"));
            }
        }
        return [.. list.DistinctBy(e => e.Url)];
    }

    public static ConnectionInfo Connection() => new(NetworkIdentity.ServerId, LocalBroadcast.NameFor(S.BroadcastName), Fingerprint(), Endpoints(),
        S.RemoteAccess.Enabled && S.RemoteAccess.PublishPkarr && Automatic ? NetworkIdentity.PkarrPublicKey : null, Status);

    /// <summary>
    /// A sign-in or invite link for a QR code: "{home address}/auth/{page}?code=…", which older apps understand, plus the server
    /// id (id), certificate fingerprint (fp), every address (e), the Pkarr key (pk) and the name (n) for newer ones.
    /// </summary>
    public static string BuildLink(string page, string code)
    {
        var endpoints = Endpoints();
        return $"{Home(endpoints)}/auth/{page}?code={Uri.EscapeDataString(code)}&{Hints(endpoints)}";
    }

    /// <summary>
    /// "Connect to this server" for a QR code: the home address with the same id / fp / e / pk / n as an invite, but no code.
    /// Apps open the sign-in screen with the server filled in. A phone camera opens it as a normal web address.
    /// </summary>
    public static string BuildServerLink()
    {
        var endpoints = Endpoints();
        return $"{Home(endpoints)}/?{Hints(endpoints)}";
    }

    private static string Hints(ServerEndpoint[] endpoints)
    {
        var s = S;
        var fingerprint = Fingerprint();
        var q = new List<string> { "v=2", "id=" + NetworkIdentity.ServerId };
        if (fingerprint != null) q.Add("fp=" + fingerprint);
        if (endpoints.Length > 0) q.Add("e=" + string.Join(",", endpoints.Select(e => Uri.EscapeDataString(e.Url))));
        if (s.RemoteAccess.Enabled && s.RemoteAccess.PublishPkarr && Automatic) q.Add("pk=" + NetworkIdentity.PkarrPublicKey);
        q.Add("n=" + Uri.EscapeDataString(LocalBroadcast.NameFor(s.BroadcastName)));
        return string.Join("&", q);
    }

    private static string Home(ServerEndpoint[] endpoints)
    {
        var s = S;
        // Invites get sent to friends who aren't home: a reverse proxy address (normal certificate) works for every app from
        // anywhere, so it goes first. Otherwise the home address; newer apps also try e= and pk= from wherever they are.
        string? home = endpoints.FirstOrDefault(e => e.Kind == "custom")?.Url;
        home ??= Automatic ? NetAddresses.LanIPv4().Select(ip => NetAddresses.Url("http", ip, s.HttpPort)).FirstOrDefault() : null;
        home ??= endpoints.FirstOrDefault()?.Url ?? (Automatic ? $"http://localhost:{s.HttpPort}" : "http://localhost:14524");
        return home.TrimEnd('/');
    }
}
