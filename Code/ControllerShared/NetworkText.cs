using Melon.Interface;

namespace Melon.Controllers;

/// <summary>Plain-language wording for networking and Remote Access, shared by MelonCLI and MelonGUI (translate with UiStrings).</summary>
internal static class NetworkText
{
    public const string CaddyGuide = "https://melon.docs.epsirho.com/Guides/Installation.html#caddy";
    public const string RemoteGuide = "https://melon.docs.epsirho.com/Guides/RemoteAccess.html";
    public const string TailscaleDownload = "https://tailscale.com/download";

    public const string IntroTitle = "Connecting your devices";
    public const string IntroHome = "Melon works with any device on the same network as this computer. Phones, tablets and other computers on your Wi-Fi can find Melon by themselves, or sign in by scanning a QR code from Users → Create invite.";
    public const string IntroAway = "To listen away from home, set up Remote Access. Melon can do it for you now, or you can start it later from the Networking page.";

    public const string Warning = "This allows clients to connect from outside your network. Melon is built to be secure, but unknown vulnerabilities could let attackers use Melon to access your system. While enabled, Security Logs are kept to keep track of who is sending what, which allows Melon to suggest blocking IPs that look suspect.";

    public const string RouterQuestion = "Try to automatically open your router port?";
    public const string RouterHelp = "This allows Melon to be open to access from outside your network.";
    public const string IPv6Question = "Enable IPv6?";
    public const string IPv6Help = "This enables Melon to get around certain IPv4 limitations such as CGNAT.";
    public const string PkarrQuestion = "Publish Melon's address through Pkarr?";
    public const string PkarrHelp = "This enables Melon to work around dynamic IP addresses. If your IP is dynamic and this is turned off, Melon clients will sometimes need to be brought back onto your local network to resync to the server.";

    public const string BroadcastHelp = "Devices on your network look for the service type _melon._tcp. The name is what they show, and it's what tells two Melon servers on one network apart.";
    public const string FirewallHelp = "Windows blocks other devices from reaching Melon until it's allowed through Windows Firewall.";
    public const string ServerQrHelp = "Scan this with Galia or Watermelon to connect to this server, then sign in. It holds the server's addresses, not a sign-in code. To add someone new, make an invite in Users instead.";

    /// <summary>Whether a friend can use an invite from their own home, given how the server is set up.</summary>
    public static string InviteReach(NetworkOverview net) =>
        net.Mode != "Automatic" ? "Apps use the invite's address to find Melon, so it works wherever that address does."
        : net.Remote.Enabled ? "The QR code and link carry Melon's addresses, so apps find it from home or away" + (net.Remote.Pkarr ? ", even after your home address changes." : ".")
        : "Remote Access is off, so this invite only works on your home network. To invite someone who isn't home, set up Remote Access in Networking first.";

    public static string State(RemoteAccessStatus s) => s.State switch
    {
        "Working" => "On · working",
        "Waiting" => "On · waiting for a first outside connection",
        "Problem" => "On · needs attention",
        "Starting" => "Starting…",
        "Custom" => "Custom setup",
        _ => "Off",
    };

    public static string Method(string method) => method switch
    {
        "Automatic" => "Automatic",
        "Tailscale" => "Tailscale",
        "ReverseProxy" => "Reverse proxy",
        "Custom" => "Custom listening URL",
        _ => "Off",
    };

    public static string Kind(string kind) => kind switch
    {
        "lan" => "Home network",
        "lan6" => "Home network (IPv6)",
        "mdns" => "Home network name",
        "public" => "Internet",
        "public6" => "Internet (IPv6)",
        "tailscale" => "Tailscale",
        "custom" => "Custom",
        _ => kind,
    };

    public static string Level(string level) => level switch
    {
        "Required" => "Required",
        "Recommended" => "Recommended",
        "Unavailable" => "Not available",
        _ => "Optional",
    };

    public static NetworkRecommendation? For(NetworkDiagnostics? d, string option) => d?.Recommendations.FirstOrDefault(r => r.Option == option);

    /// <summary>What the network check found, one short fact each. Warn = this fact limits what Remote Access can do.</summary>
    public static (string Label, string Value, bool Warn)[] Facts(NetworkDiagnostics d)
    {
        var facts = new List<(string, string, bool)>
        {
            ("Public address", d.PublicIPv4 ?? "Not found", d.PublicIPv4 == null),
            ("Shared address (CGNAT)", d.Cgnat ? "Yes" : "No", d.Cgnat),
            ("Routers", d.DoubleNat ? "Two (double NAT)" : "One", d.DoubleNat),
            ("Automatic port opening", d.RouterFound ? "Yes (" + d.RouterProtocol + ")" : "No", !d.RouterFound),
            ("IPv6", d.IPv6Works ? "Works" : "No", false),
            ("Address changes", d.AddressKind switch { "Dynamic" => "Yes", "Static" => "No", _ => "Can't tell yet" }, false),
        };
        if (d.TailscaleAddress != null) facts.Add(("Tailscale", d.TailscaleName ?? d.TailscaleAddress, false));
        return [.. facts];
    }

    /// <summary>"Automatic · 1.2.3.4:14525 · router port · IPv6 · Pkarr": how Remote Access is set up, in one line.</summary>
    public static string Line(RemoteAccessStatus s)
    {
        var parts = new List<string> { Method(s.Method) };
        if (s.PublicAddress != null) parts.Add(s.PublicAddress);
        if (s.Method == "Automatic")
        {
            if (s.RouterPort) parts.Add("router port " + s.ExternalPort);
            if (s.IPv6) parts.Add("IPv6");
        }
        if (s.Pkarr) parts.Add("Pkarr");
        return string.Join(" · ", parts);
    }

    public static string LastOutside(RemoteAccessStatus s) =>
        s.LastOutsideUtc is { } t ? "Last outside connection: " + t.ToLocalTime().ToString("g") : "No outside connection yet.";
}
