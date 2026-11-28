using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Melon.LocalClasses.Networking;

/// <summary>Where an address is: this computer, the home network, a private overlay (Tailscale, CGNAT range) or the internet.</summary>
public enum AddressScope { Loopback, Local, Overlay, Public }

public static class NetAddresses
{
    public static AddressScope Scope(IPAddress? address)
    {
        if (address == null) return AddressScope.Public;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return AddressScope.Loopback;
        var own = Own();
        // An app on this computer that connects to one of its own addresses (say its public IPv6) isn't outside.
        if (own.Addresses.Contains(address)) return AddressScope.Loopback;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            if (b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168 || b[0] == 169 && b[1] == 254) return AddressScope.Local;
            if (b[0] == 100 && b[1] is >= 64 and <= 127) return AddressScope.Overlay;
            return AddressScope.Public;
        }
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || IsUniqueLocal(address)) return AddressScope.Local;
        // Tailscale's IPv6 range, fd7a:115c:a1e0::/48, is unique-local and so already Local above.
        // Devices on the home network have public IPv6 addresses too, in the same /64 as this computer's.
        if (IsGlobal6(address) && own.Prefixes6.Any(p => Matches(address, p, 64))) return AddressScope.Local;
        return AddressScope.Public;
    }

    private static (HashSet<IPAddress> Addresses, IPAddress[] Prefixes6, DateTime At) own = ([], [], DateTime.MinValue);
    private static readonly object OwnGate = new();

    /// <summary>The network changed: look up this computer's addresses again on the next request instead of up to 30 seconds later.</summary>
    public static void Forget() { lock (OwnGate) own = ([], [], DateTime.MinValue); }

    /// <summary>This computer's own addresses and its global IPv6 /64s, looked up at most every 30 seconds.</summary>
    private static (HashSet<IPAddress> Addresses, IPAddress[] Prefixes6) Own()
    {
        lock (OwnGate)
        {
            if (DateTime.UtcNow - own.At < TimeSpan.FromSeconds(30)) return (own.Addresses, own.Prefixes6);
            var addresses = new HashSet<IPAddress>();
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
                    foreach (var u in n.GetIPProperties().UnicastAddresses) addresses.Add(u.Address);
            }
            catch (NetworkInformationException) { }
            var prefixes = addresses.Where(IsGlobal6).Select(a => { var b = a.GetAddressBytes(); Array.Clear(b, 8, 8); return new IPAddress(b); })
                .Distinct().ToArray();
            own = (addresses, prefixes, DateTime.UtcNow);
            return (addresses, prefixes);
        }
    }

    public static bool IsCgnat(IPAddress a)
    {
        if (a.AddressFamily == AddressFamily.InterNetworkV6 && !a.IsIPv4MappedToIPv6) return false;
        var b = a.MapToIPv4().GetAddressBytes();
        return b[0] == 100 && b[1] is >= 64 and <= 127;
    }
    public static bool IsPrivate4(IPAddress a)
    {
        if (a.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = a.GetAddressBytes();
        return b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168;
    }
    private static bool IsUniqueLocal(IPAddress a) => a.AddressFamily == AddressFamily.InterNetworkV6 && (a.GetAddressBytes()[0] & 0xFE) == 0xFC;
    public static bool IsGlobal6(IPAddress a) => a.AddressFamily == AddressFamily.InterNetworkV6 && (a.GetAddressBytes()[0] & 0xE0) == 0x20;

    private static bool IsTailscale(NetworkInterface n) =>
        n.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) || n.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) ||
        n.Name.StartsWith("tailscale", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<NetworkInterface> Up() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel));

    /// <summary>Home network IPv4 addresses, best first: interfaces with a default gateway (real networks, not virtual switches).</summary>
    public static IPAddress[] LanIPv4()
    {
        try
        {
            return Up().Where(n => !IsTailscale(n))
                .SelectMany(n =>
                {
                    var p = n.GetIPProperties();
                    bool gateway = p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                    return p.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate4(u.Address))
                        .Select(u => (u.Address, gateway, wired: n.NetworkInterfaceType == NetworkInterfaceType.Ethernet));
                })
                .ToList() is var all && all.Any(x => x.gateway)
                    ? all.Where(x => x.gateway).OrderByDescending(x => x.wired).Select(x => x.Address).Distinct().ToArray()
                    : all.Select(x => x.Address).Distinct().ToArray(); // no gateway anywhere: an offline network, keep everything
        }
        catch (NetworkInformationException) { return []; }
    }

    /// <summary>Global IPv6 addresses on interfaces with an IPv6 gateway, stable ones first (privacy addresses change daily).</summary>
    public static IPAddress[] GlobalIPv6()
    {
        try
        {
            return Up().Where(n => !IsTailscale(n))
                .Where(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetworkV6))
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(u => IsGlobal6(u.Address))
                // Stable addresses first: manual / DHCPv6 / EUI-64, then the longest-lived. Temporary privacy addresses change daily.
                .OrderBy(u => Stability(u))
                .ThenByDescending(u => Lifetime(u))
                .Select(u => u.Address).Distinct().ToArray();
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException) { return []; }
    }

    private static int Stability(UnicastIPAddressInformation u)
    {
        try { return u.SuffixOrigin switch { SuffixOrigin.Manual => 0, SuffixOrigin.OriginDhcp => 1, SuffixOrigin.LinkLayerAddress => 2, _ => 3 }; }
        catch (PlatformNotSupportedException) { return 3; }
    }
    private static long Lifetime(UnicastIPAddressInformation u)
    {
        try { return u.AddressPreferredLifetime; } catch (PlatformNotSupportedException) { return 0; }
    }

    /// <summary>This computer's Tailscale IPv4 address, when Tailscale is running.</summary>
    public static IPAddress? Tailscale()
    {
        try
        {
            return Up().Where(IsTailscale).SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && IsCgnat(a));
        }
        catch (NetworkInformationException) { return null; }
    }

    /// <summary>Whether the Tailscale app is installed, even if it isn't connected right now.</summary>
    public static bool TailscaleInstalled()
    {
        if (Tailscale() != null) return true;
        try
        {
            if (OperatingSystem.IsWindows())
                return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe"));
            return File.Exists("/usr/bin/tailscale") || File.Exists("/usr/local/bin/tailscale") || File.Exists("/Applications/Tailscale.app/Contents/MacOS/Tailscale");
        }
        catch (Exception) { return false; }
    }

    /// <summary>Tailscale's name for this computer (MagicDNS, like "pc.tail1234.ts.net"), from the reverse lookup of its address.</summary>
    public static async Task<string?> TailscaleName(IPAddress address, CancellationToken token)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(address.ToString(), token).WaitAsync(TimeSpan.FromSeconds(2), token);
            return entry.HostName.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase) ? entry.HostName.TrimEnd('.') : null;
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or ArgumentException) { return null; }
    }

    /// <summary>"https://192.168.1.2:14525" / "https://[2601::1]:14525".</summary>
    public static string Url(string scheme, IPAddress address, int port) =>
        $"{scheme}://{(address.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + address + "]" : address.ToString())}:{port}";

    public static bool TryParseBlock(string text, out IPAddress network, out int prefix)
    {
        prefix = -1; network = IPAddress.None;
        text = text.Trim();
        var slash = text.IndexOf('/');
        if (!IPAddress.TryParse(slash < 0 ? text : text[..slash], out var parsed)) return false;
        if (parsed.IsIPv4MappedToIPv6) parsed = parsed.MapToIPv4();
        int max = parsed.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (slash < 0) prefix = max;
        else if (!int.TryParse(text[(slash + 1)..], out prefix) || prefix < 0 || prefix > max) return false;
        network = parsed;
        return true;
    }

    public static bool Matches(IPAddress address, IPAddress network, int prefix)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != network.AddressFamily) return false;
        var a = address.GetAddressBytes(); var n = network.GetAddressBytes();
        int full = prefix / 8, rest = prefix % 8;
        for (int i = 0; i < full; i++) if (a[i] != n[i]) return false;
        if (rest == 0) return true;
        int mask = 0xFF << (8 - rest) & 0xFF;
        return (a[full] & mask) == (n[full] & mask);
    }
}
