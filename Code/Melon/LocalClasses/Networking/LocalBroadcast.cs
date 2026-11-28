using System.Net;
using Makaretu.Dns;
using Serilog;

namespace Melon.LocalClasses.Networking;

/// <summary>
/// Advertises Melon on the local network with mDNS / DNS-SD, so apps on the same Wi-Fi find it without typing an address.
/// The service type is always _melon._tcp (that's what apps look for); the instance name is what people see, and is what
/// tells two Melon servers on one network apart.
/// </summary>
public static class LocalBroadcast
{
    public const string ServiceType = "_melon._tcp";
    private static readonly object Gate = new();
    private static MulticastService? mdns;
    private static ServiceDiscovery? discovery;
    private static ServiceProfile? profile;
    public static string? Problem { get; private set; }

    public static string DefaultName => "Melon on " + Environment.MachineName;
    public static string NameFor(string configured) => string.IsNullOrWhiteSpace(configured) ? DefaultName : configured.Trim();

    /// <summary>Starts (or restarts with new details) the advertisement. TXT: id, fp, https and http ports, version.</summary>
    public static void Start(string name, int httpsPort, int httpPort, string serverId, string? fingerprint)
    {
        lock (Gate)
        {
            StopLocked();
            try
            {
                mdns = new MulticastService();
                discovery = new ServiceDiscovery(mdns);
                // Only real home-network addresses, so apps don't try virtual adapters (WSL, Hyper-V, VPNs) first.
                var lan = NetAddresses.LanIPv4();
                profile = lan.Length > 0
                    ? new ServiceProfile(new DomainName(Escape(name)), new DomainName(ServiceType), (ushort)httpsPort, lan)
                    : new ServiceProfile(new DomainName(Escape(name)), new DomainName(ServiceType), (ushort)httpsPort);
                profile.AddProperty("v", "1");
                profile.AddProperty("id", serverId);
                profile.AddProperty("https", httpsPort.ToString());
                if (httpPort > 0) profile.AddProperty("http", httpPort.ToString());
                if (fingerprint != null) profile.AddProperty("fp", fingerprint);
                profile.AddProperty("ver", StateManager.Version ?? "");
                discovery.Advertise(profile);
                mdns.Start();
                discovery.Announce(profile);
                Problem = null;
                Log.Information("Broadcasting \"{Name}\" on the local network ({Type})", name, ServiceType);
            }
            catch (Exception ex)
            {
                Problem = "Melon couldn't broadcast on the local network: " + ex.Message;
                Log.Warning("Local network broadcast unavailable: {Message}", ex.Message);
                StopLocked();
            }
        }
    }

    public static void Stop() { lock (Gate) StopLocked(); }

    private static void StopLocked()
    {
        try { if (discovery != null && profile != null) discovery.Unadvertise(profile); } catch (Exception) { }
        try { discovery?.Dispose(); } catch (Exception) { }
        try { mdns?.Stop(); mdns?.Dispose(); } catch (Exception) { }
        discovery = null; mdns = null; profile = null;
    }

    /// <summary>Instance names are one DNS label: dots would split it.</summary>
    private static string Escape(string name) => name.Replace(".", " ").Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 63)] : "Melon";
}
