using Melon.Interface;
using Melon.LocalClasses.Networking;
using Melon.Models;
using static Melon.LocalClasses.StateManager;

namespace Melon.LocalClasses;

public sealed partial class ServerAdministration
{
    private static readonly string[] Methods = ["Automatic", "Tailscale", "ReverseProxy"];

    public NetworkOverview GetNetwork()
    {
        var s = MelonSettings;
        bool automatic = s.NetworkMode == "Automatic";
        return new(automatic ? "Automatic" : "Custom", s.HttpPort, s.HttpsPort, s.ListeningURL, Security.GetSSLConfig()?.PathToCert,
            RemoteAccessService.Fingerprint(), s.Broadcast, s.BroadcastName ?? "", LocalBroadcast.NameFor(s.BroadcastName ?? ""), LocalBroadcast.ServiceType,
            LocalBroadcast.Problem, RemoteAccessService.Endpoints(), RemoteAccessService.Status, RemoteAccessService.LastDiagnostics,
            [.. s.BlockedAddresses.Select(b => b.Address)], automatic && s.RemoteAccess.PublishPkarr ? NetworkIdentity.PkarrPublicKey : null,
            NetAddresses.TailscaleInstalled(), RemoteAccessService.BuildServerLink());
    }

    public void UseAutomaticNetworking(int httpPort = 14524, int httpsPort = 14525)
    {
        if (httpPort is < 1 or > 65535 || httpsPort is < 1 or > 65535) throw new ArgumentException("Ports must be 1–65535.");
        if (httpPort == httpsPort) throw new ArgumentException("The HTTP and HTTPS ports must be different.");
        _ = NetworkIdentity.Certificate; // made now, so a broken certificate store shows up here instead of at restart
        ChangeSettings(s => { s.NetworkMode = "Automatic"; s.HttpPort = httpPort; s.HttpsPort = httpsPort; });
        RemoteAccessService.Kick();
    }

    public void UseCustomNetworking(string listeningUrl, string certificatePath, string certificatePassword)
    {
        foreach (var part in listeningUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var probe = part.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:");
            if (!Uri.TryCreate(probe, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                || uri.UserInfo.Length != 0 || uri.PathAndQuery != "/" || uri.Fragment.Length != 0)
                throw new ArgumentException("Expected an HTTP or HTTPS listening address.");
        }
        bool https = listeningUrl.Contains("https:", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(certificatePath)) SetCertificate(certificatePath, certificatePassword ?? "");
        else if (https && string.IsNullOrEmpty(Security.GetSSLConfig()?.PathToCert)) throw new ArgumentException("An HTTPS listening address needs a certificate.");
        else if (!https) DisableCertificate();
        if (MelonSettings.RemoteAccess.Enabled) _ = DisableRemoteAccess();
        ChangeSettings(s => { s.NetworkMode = "Custom"; s.ListeningURL = listeningUrl.Trim(); });
        RemoteAccessService.Kick();
    }

    public void SetBroadcast(bool enabled, string name)
    {
        name = (name ?? "").Trim();
        if (name.Length > 63) throw new ArgumentException("The broadcast name can be at most 63 characters.");
        if (name.Any(char.IsControl) || name.Contains('.')) throw new ArgumentException("The broadcast name can't contain dots.");
        ChangeSettings(s => { s.Broadcast = enabled; s.BroadcastName = name; });
        RemoteAccessService.UpdateBroadcast(MelonSettings);
    }

    public async Task<NetworkDiagnostics> DiagnoseNetwork()
    {
        var found = await NetworkDiagnoser.Run(MelonSettings.PublicAddressHistory, CancellationToken.None);
        if (found.PublicIPv4 != null) RecordPublicAddress(found.PublicIPv4);
        RemoteAccessService.LastDiagnostics = found;
        return found;
    }

    /// <summary>Keeps the last 10 different public addresses, to tell whether the address changes.</summary>
    internal void RecordPublicAddress(string address)
    {
        var history = MelonSettings.PublicAddressHistory;
        if (history.Count > 0 && history[^1] == address) return;
        ChangeSettings(s => { s.PublicAddressHistory = [.. s.PublicAddressHistory.Where(a => a != address).TakeLast(9), address]; });
    }

    public async Task<RemoteAccessStatus> EnableRemoteAccess(RemoteAccessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (MelonSettings.NetworkMode != "Automatic")
            throw new InvalidOperationException("Remote Access needs automatic networking. Switch to it first, or keep using your custom setup.");
        if (!Methods.Contains(options.Method)) throw new ArgumentException("Choose Automatic, Tailscale or ReverseProxy.");
        if (options.ExternalPort is < 0 or > 65535) throw new ArgumentException("Ports must be 1–65535.");
        string? publicUrl = null;
        if (options.Method == "ReverseProxy")
        {
            if (!Uri.TryCreate(options.PublicUrl?.Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http") || u.PathAndQuery != "/")
                throw new ArgumentException("Enter the address clients use, such as https://music.example.com.");
            publicUrl = u.GetLeftPart(UriPartial.Authority);
        }
        if (options.Method == "Tailscale" && !NetAddresses.TailscaleInstalled()) throw new InvalidOperationException("Tailscale isn't installed on this computer.");
        bool wasOn = MelonSettings.RemoteAccess.Enabled;
        ChangeSettings(s => s.RemoteAccess = new RemoteAccessSettings
        {
            Enabled = true, Method = options.Method, OpenRouterPort = options.Method == "Automatic" && options.OpenRouterPort,
            UseIPv6 = options.Method == "Automatic" && options.UseIPv6, PublishPkarr = options.PublishPkarr, ExternalPort = options.ExternalPort,
            PublicUrl = publicUrl, PkarrRelays = s.RemoteAccess.PkarrRelays,
        });
        SecurityLog.Record(SecurityEventKinds.RemoteAccessEnabled, detail: $"{options.Method}{(options.OpenRouterPort ? ", router port" : "")}{(options.UseIPv6 ? ", IPv6" : "")}{(options.PublishPkarr ? ", Pkarr" : "")}{(wasOn ? " (changed)" : "")}");
        var result = await RemoteAccessService.Cycle(CancellationToken.None);
        Messages.Publish("settings", "Remote Access changed.");
        return result;
    }

    public async Task DisableRemoteAccess()
    {
        var old = MelonSettings.RemoteAccess;
        ChangeSettings(s => s.RemoteAccess = new RemoteAccessSettings
        {
            Enabled = false, Method = old.Method, OpenRouterPort = old.OpenRouterPort, UseIPv6 = old.UseIPv6, PublishPkarr = old.PublishPkarr,
            ExternalPort = old.ExternalPort, PublicUrl = old.PublicUrl, PkarrRelays = old.PkarrRelays,
        });
        await RemoteAccessService.Cycle(CancellationToken.None);
        if (old.Enabled) SecurityLog.Record(SecurityEventKinds.RemoteAccessDisabled);
        Messages.Publish("settings", "Remote Access changed.");
    }

    public SecuritySuggestion[] GetSecuritySuggestions() => NetworkGuard.Suggestions();
    public RemoteClient[] GetRemoteClients() => NetworkGuard.Clients();
    public void DismissSuggestion(string address) => NetworkGuard.Dismiss(address);

    public void BlockAddress(string address, string reason)
    {
        if (!NetAddresses.TryParseBlock(address ?? "", out var network, out var prefix)) throw new ArgumentException("Enter an IP address or a range like 203.0.113.0/24.");
        if (NetAddresses.Scope(network) == AddressScope.Loopback) throw new ArgumentException("This computer's own address can't be blocked.");
        var text = prefix == (network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128) ? network.ToString() : $"{network}/{prefix}";
        if (MelonSettings.BlockedAddresses.Any(b => b.Address == text)) return;
        ChangeSettings(s => s.BlockedAddresses = [.. s.BlockedAddresses, new BlockedAddress { Address = text, Reason = (reason ?? "").Trim(), BlockedUtc = DateTime.UtcNow }]);
        SecurityLog.Record(SecurityEventKinds.AddressBlocked, address: text, detail: reason);
    }

    public void UnblockAddress(string address)
    {
        var text = (address ?? "").Trim();
        if (!MelonSettings.BlockedAddresses.Any(b => b.Address == text)) return;
        ChangeSettings(s => s.BlockedAddresses = [.. s.BlockedAddresses.Where(b => b.Address != text)]);
        SecurityLog.Record(SecurityEventKinds.AddressUnblocked, address: text);
    }
}
