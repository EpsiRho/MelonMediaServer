using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Melon.Interface;

namespace Melon.LocalClasses.Networking;

/// <summary>Looks at the connection before Remote Access is turned on, and says which options this network needs.</summary>
public static partial class NetworkDiagnoser
{
    public static async Task<NetworkDiagnostics> Run(IReadOnlyList<string> addressHistory, CancellationToken token)
    {
        var lan = NetAddresses.LanIPv4();
        var publicV4 = Stun.PublicAddress(AddressFamily.InterNetwork, token);
        var publicV6 = Socket.OSSupportsIPv6 ? Stun.PublicAddress(AddressFamily.InterNetworkV6, token) : Task.FromResult<IPAddress?>(null);
        var routerTask = PortMapper.FindRouter(TimeSpan.FromSeconds(4), token);
        var hopsTask = Hops(token);
        await Task.WhenAll(publicV4, publicV6, routerTask, hopsTask);
        var router = routerTask.Result;
        var wan = router != null ? await PortMapper.RouterWanAddress(router) : null;
        var v4 = publicV4.Result; var hops = hopsTask.Result;
        var notes = new List<string>();

        bool cgnat = wan != null && NetAddresses.IsCgnat(wan)
            || wan != null && NetAddresses.Scope(wan) == AddressScope.Public && v4 != null && !wan.Equals(v4)
            || wan == null && hops.Any(NetAddresses.IsCgnat);
        bool doubleNat = wan != null && NetAddresses.IsPrivate4(wan)
            || wan == null && hops.Count >= 2 && NetAddresses.IsPrivate4(hops[0]) && NetAddresses.IsPrivate4(hops[1]);
        if (wan == null && doubleNat) notes.Add("The first two hops toward the internet are both private addresses, which usually means two routers in a row.");
        if (cgnat && wan == null) notes.Add("An address in the 100.64.0.0/10 range showed up on the way to the internet, which usually means the provider uses CGNAT.");

        // IPv6 only helps when the address STUN sees is one this computer actually has (no translation in between).
        var localV6 = NetAddresses.GlobalIPv6();
        var seenV6 = publicV6.Result;
        bool v6Works = seenV6 != null && localV6.Any(a => a.Equals(seenV6));
        // Outgoing traffic uses a temporary address; show the stable one, which is what Melon advertises.
        var v6 = localV6.FirstOrDefault() ?? seenV6;
        if (seenV6 != null && !v6Works) notes.Add("IPv6 reaches the internet through a translator, so it can't be used to reach Melon.");
        if (v6Works) notes.Add("Some routers block incoming IPv6 connections by default. If clients can't connect over IPv6, allow Melon's HTTPS port in the router's IPv6 firewall.");

        var (kind, reason) = await AddressKind(v4, addressHistory, token);

        var tailscale = NetAddresses.Tailscale();
        var tailscaleName = tailscale != null ? await NetAddresses.TailscaleName(tailscale, token) : null;

        var r = new List<NetworkRecommendation>();
        if (cgnat)
            r.Add(new("RouterPort", "Unavailable", "Your internet provider shares one public address between many customers (CGNAT), so opening a port on your router can't make Melon reachable over IPv4."));
        else if (doubleNat)
            r.Add(new("RouterPort", router != null ? "Optional" : "Unavailable", "There are two routers in a row. Melon can only open a port on the one it's connected to; the other one needs the same port forwarded by hand, or to be put in bridge mode."));
        else if (router != null)
            r.Add(new("RouterPort", "Recommended", $"Your router supports automatic port opening ({PortMapper.ProtocolName(router)})."));
        else
            r.Add(new("RouterPort", "Optional", "Your router didn't answer. Its automatic port opening (UPnP) may be off: turn it on in the router's settings, or forward the port by hand."));

        if (!v6Works)
            r.Add(new("IPv6", "Unavailable", seenV6 == null ? "This network doesn't have working IPv6." : "IPv6 goes through a translator on this network."));
        else if (cgnat || doubleNat && router == null)
            r.Add(new("IPv6", "Required", "IPv6 gets around " + (cgnat ? "CGNAT" : "the second router") + ": your devices can reach Melon directly over IPv6. Apps need IPv6 too (most phone networks have it)."));
        else
            r.Add(new("IPv6", "Optional", "IPv6 works here. It's another way in if the router port doesn't work."));

        r.Add(kind == "Static"
            ? new("Pkarr", "Optional", "Your public address looks fixed, so apps can keep using it. Pkarr is a backup in case it changes.")
            : new("Pkarr", "Recommended", kind == "Dynamic"
                ? "Your public address changes from time to time. With Pkarr, apps find the new address by themselves."
                : "Most home connections change their public address now and then. With Pkarr, apps find the new address by themselves."));

        bool noWayIn = cgnat && !v6Works;
        r.Add(tailscale != null
            ? new("Tailscale", noWayIn ? "Recommended" : "Optional", "Tailscale is running on this computer" + (noWayIn ? " and works on this network when nothing else does." : "."))
            : NetAddresses.TailscaleInstalled()
                ? new("Tailscale", noWayIn ? "Recommended" : "Optional", "Tailscale is installed but not connected. Sign in to Tailscale to use it.")
                : new("Tailscale", noWayIn ? "Recommended" : "Unavailable", noWayIn
                    ? "Nothing else can get through on this network. Tailscale (free) connects your devices privately: install it here and on your phone."
                    : "Tailscale isn't installed."));
        if (noWayIn) notes.Add("On this connection, the only ways in are Tailscale or a reverse proxy/tunnel set up under Advanced.");

        return new NetworkDiagnostics(DateTime.UtcNow, v4?.ToString(), wan?.ToString(), router != null, router != null ? PortMapper.ProtocolName(router) : null,
            cgnat, doubleNat, v6?.ToString(), v6Works, kind, reason, tailscale?.ToString(), tailscaleName,
            [.. hops.Select(h => h.ToString())], [.. lan.Select(a => a.ToString())], [.. r], [.. notes]);
    }

    /// <summary>The first few routers on the way to the internet (TTL-limited pings).</summary>
    private static async Task<List<IPAddress>> Hops(CancellationToken token)
    {
        var hops = new List<IPAddress>();
        try
        {
            using var ping = new Ping();
            var target = IPAddress.Parse("1.1.1.1");
            for (int ttl = 1; ttl <= 5 && !token.IsCancellationRequested; ttl++)
            {
                var reply = await ping.SendPingAsync(target, TimeSpan.FromMilliseconds(1200), new byte[16], new PingOptions(ttl, true), token);
                if (reply.Status == IPStatus.TtlExpired && reply.Address != null) hops.Add(reply.Address);
                else if (reply.Status == IPStatus.Success) break;
                else if (ttl == 1) break; // no answer from the first hop: ICMP is blocked, nothing to learn
            }
        }
        catch (Exception ex) when (ex is PingException or PlatformNotSupportedException or InvalidOperationException or OperationCanceledException) { }
        return hops;
    }

    [GeneratedRegex(@"(^|[.\-])(dyn|dynamic|dhcp|pool|cpe|res|resi|residential|broadband|cable|dsl|adsl|ppp|pppoe|client|user|home|ip\d+|\d+-\d+-\d+-\d+)([.\-\d]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex DynamicName();
    [GeneratedRegex(@"(^|[.\-])(static|stat|fixed|biz|business|srv|server|colo|dedicated)([.\-\d]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex StaticName();

    private static async Task<(string Kind, string? Reason)> AddressKind(IPAddress? v4, IReadOnlyList<string> history, CancellationToken token)
    {
        if (v4 != null && history.Any(h => h != v4.ToString())) return ("Dynamic", "Your public address has changed since Melon last looked.");
        if (v4 == null) return ("Unknown", null);
        try
        {
            var name = (await Dns.GetHostEntryAsync(v4.ToString(), token).WaitAsync(TimeSpan.FromSeconds(3), token)).HostName;
            if (StaticName().IsMatch(name)) return ("Static", $"Your provider names this address \"{name}\", which usually means a fixed address.");
            if (DynamicName().IsMatch(name)) return ("Dynamic", $"Your provider names this address \"{name}\", which usually means it changes.");
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or ArgumentException) { }
        return ("Unknown", "Melon can't tell yet whether your address changes. It keeps checking.");
    }
}
