using Melon.Controllers;
using Melon.Interface;
using Melon.Models;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private bool networkIntro;
    private bool? firewallAllowed;

    private static int[] Ports(NetworkOverview net)
    {
        if (net.Mode == "Automatic") return [net.HttpPort, net.HttpsPort];
        return net.ListeningUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => Uri.TryCreate(u.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:"), UriKind.Absolute, out var uri) ? uri.Port : 0)
            .Where(p => p > 0).Distinct().ToArray();
    }

    /// <summary>Null when this isn't Windows (no Windows Firewall to set up).</summary>
    private async Task<bool?> FirewallAllowed()
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (firewallAllowed is bool known) return known;
        try { firewallAllowed = await WindowsHost.FirewallRuleExists(token); }
        catch (Exception ex) when (ex is not OperationCanceledException) { firewallAllowed = true; }
        return firewallAllowed;
    }

    private async Task AllowFirewall(NetworkOverview net)
    {
        try { await WindowsHost.AllowThroughFirewall(Ports(net), token); }
        catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException(T("Windows Firewall wasn't changed.")); }
        firewallAllowed = true;
        Say(T("Melon is allowed through Windows Firewall."));
    }

    /// <summary>Yes / No, with the details shown above. Returns false for No.</summary>
    private async Task<bool> YesNo(string key, params Block[] details)
    {
        bool yes = false;
        var screen = new Screen(T(key));
        screen.PanelBlocks.AddRange(details);
        screen.Items.Add(Local(T("Yes"), () => yes = true));
        screen.Items.Add(Local(T("No"), () => { }));
        var item = await ui.Menu(screen);
        if (item == null) return false;
        await item.Run!();
        return yes;
    }

    private static Tone LevelTone(string? level) => level switch
    { "Required" => Tone.Warn, "Recommended" => Tone.Good, "Unavailable" => Tone.Danger, _ => Tone.Muted };

    /// <summary>A QR code with just the server: apps fill in its address and how to find it, then you sign in as usual.</summary>
    private async Task ServerQr(NetworkOverview net)
    {
        var link = net.ServerLink ?? net.Endpoints.FirstOrDefault()?.Url;
        if (link == null) { Say(T("Melon doesn't have an address other devices can use yet.")); return; }
        var screen = new Screen(T("Server QR code"));
        screen.Body.Add(new QrBlock(Net.Codecrete.QrCodeGenerator.QrCode.EncodeText(link, Net.Codecrete.QrCodeGenerator.QrCode.Ecc.Medium)));
        screen.Body.Add(new Para(T(NetworkText.ServerQrHelp), Tone.Muted));
        screen.Body.Add(new Para(link, Tone.Muted));
        screen.Items.Add(Action("Copy link", async () => { await Platform.Copy(link, token); Say(T("Copied")); }));
        var item = await ui.Menu(screen);
        if (item != null) await item.Run!();
    }

    private Task Addresses(NetworkOverview net)
    {
        var rows = net.Endpoints.Select(e => (T(NetworkText.Kind(e.Kind)), e.Url, Tone.Normal)).ToList();
        if (net.Fingerprint != null) rows.Add((T("Certificate fingerprint"), net.Fingerprint, Tone.Muted));
        if (net.PkarrKey != null) rows.Add(("Pkarr " + T("key"), net.PkarrKey, Tone.Muted));
        return ShowBlocks("Addresses", new Para(T("Apps learn these from QR codes and keep them up to date, so nobody has to type them."), Tone.Muted), new KeyValues(rows));
    }

    private Tone StateTone(RemoteAccessStatus r) => r.State switch
    { "Working" => Tone.Good, "Waiting" => Tone.Info, "Problem" => Tone.Warn, _ => Tone.Muted };

    private async Task Networking(Screen page)
    {
        var net = await Read<NetworkOverview>(nameof(Admin.GetNetwork));
        bool automatic = net.Mode == "Automatic";
        var r = net.Remote;
        var firewall = await FirewallAllowed();

        page.Body.Add(new Cards([
            new Card(T("Connection"), T(automatic ? "Automatic" : "Custom"), automatic ? $"HTTP {net.HttpPort} · HTTPS {net.HttpsPort}" : net.ListeningUrl),
            new Card(T("Local Network Broadcast"), T(net.Broadcast ? "On" : "Off"), net.Broadcast ? net.BroadcastNameInUse : null, Dot: net.Broadcast && net.BroadcastProblem == null ? Tone.Good : Tone.Muted),
            new Card(T("Remote Access"), T(NetworkText.State(r)), r.Enabled ? T(NetworkText.Method(r.Method)) : null, Dot: StateTone(r))], 26));
        if (firewall == false)
            page.Body.Add(new Callout(T(NetworkText.FirewallHelp), Tone.Warn, T("Windows Firewall may block other devices")));
        if (net.BroadcastProblem != null) page.Body.Add(new Callout(T(net.BroadcastProblem), Tone.Warn));
        if (!automatic)
            page.Body.Add(new Callout(T("Melon uses your own listening URL and certificate. Apps can still connect by that address, but the local network broadcast and automatic Remote Access need automatic networking."), Tone.Info, T("Custom setup")));

        if (r.Enabled && r.State != "Custom")
        {
            var lines = new List<string> { T(NetworkText.Line(r)) };
            if (r.Detail != null) lines.Add(T(r.Detail));
            lines.Add(T(NetworkText.LastOutside(r)));
            page.Body.Add(new Panel(T("Remote Access"), new Para(string.Join("\n", lines)), T(NetworkText.State(r)), StateTone(r)));
        }

        page.Items.Add(Entry.Header(T("Connection")));
        page.Items.Add(Action("Server QR code", () => ServerQr(net)));
        page.Items.Add(new Entry(T("Addresses"), () => Addresses(net)) { Value = $"{net.Endpoints.Length:N0}" });
        if (automatic)
            page.Items.Add(Setting("Ports", $"{net.HttpPort} · {net.HttpsPort}", async () =>
            {
                if (!int.TryParse(await Ask("HTTP port (home network)", net.HttpPort.ToString()), out var http) ||
                    !int.TryParse(await Ask("HTTPS port", net.HttpsPort.ToString()), out var https))
                    throw new ArgumentException(T("Enter a valid port."));
                await Write(nameof(Admin.UseAutomaticNetworking), new { httpPort = http, httpsPort = https });
                firewallAllowed = null;
            }));
        else
        {
            page.Items.Add(Action("Use automatic networking", async () =>
            {
                await Confirm("Use automatic networking?", T("Melon will listen on ports 14524 (HTTP) and 14525 (HTTPS) with its own certificate. Your custom listening URL and certificate stop being used. Melon needs a restart after this."));
                await Write(nameof(Admin.UseAutomaticNetworking), new { httpPort = 14524, httpsPort = 14525 });
                firewallAllowed = null;
            }));
            page.Items.Add(Action("Edit custom setup", () => CustomSetup(net)));
        }
        if (firewall != null)
            page.Items.Add(Setting("Windows Firewall", T(firewall == true ? "Allowed" : "Not allowed"), () => AllowFirewall(net)) with { ValueTone = firewall == true ? Tone.Good : Tone.Warn });

        page.Items.Add(Entry.Header(T("Local Network Broadcast")));
        page.Items.Add(Setting("Broadcast", T(net.Broadcast ? "On" : "Off"), () =>
            Write(nameof(Admin.SetBroadcast), new { enabled = !net.Broadcast, name = net.BroadcastName })));
        page.Items.Add(Setting("Name", net.BroadcastNameInUse, async () =>
        {
            var name = await Ask("Name", net.BroadcastName, help: T(NetworkText.BroadcastHelp) + " " + T("Leave empty for") + " \"Melon on " + Environment.MachineName + "\".");
            await Write(nameof(Admin.SetBroadcast), new { enabled = net.Broadcast, name = name.Trim() });
        }));
        page.Items.Add(Setting("Service type", net.ServiceType, () => Show("Service type", T(NetworkText.BroadcastHelp))));

        page.Items.Add(Entry.Header(T("Remote Access")));
        if (!r.Enabled) page.Items.Add(Action("Set up Remote Access", RemoteAccessWizard) with { Tone = Tone.Accent });
        else
        {
            page.Items.Add(Action("Change Remote Access", RemoteAccessWizard));
            page.Items.Add(Action("Turn off Remote Access", async () =>
            {
                await Confirm("Turn off Remote Access?", T("Devices away from home won't be able to connect until you turn it on again. Melon closes the router port it opened."));
                await Write(nameof(Admin.DisableRemoteAccess));
            }, Tone.Warn));
        }

        page.Items.Add(Entry.Header(T("Security")));
        SecuritySuggestion[] suggestions = [];
        try { suggestions = await Read<SecuritySuggestion[]>(nameof(Admin.GetSecuritySuggestions)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        foreach (var s in suggestions)
            page.Items.Add(new Entry(T("Block") + " " + s.Address + "?", () => Suggestion(s)) { Value = T(s.Reason), ValueTone = Tone.Warn, Tone = Tone.Warn });
        if (suggestions.Length > 0)
            page.Body.Add(new Callout(T("Melon noticed addresses that look suspicious. Pick one in Security to block it or dismiss it."), Tone.Warn, $"{suggestions.Length:N0} " + T("suggestions")));
        page.Items.Add(Setting("Blocked addresses", net.BlockedAddresses.Length.ToString("N0"), () => BlockedAddresses(net)));
        page.Items.Add(Action("Outside connections", OutsideConnections));
        page.Items.Add(Action("Security events", async () =>
            await SecurityEvents(await Read<SecurityEvent[]>(nameof(Admin.GetSecurityEvents), new { count = 300, warningsOnly = false }))));
    }

    private async Task Suggestion(SecuritySuggestion s)
    {
        bool yes = false, dismiss = false;
        var screen = new Screen(T("Block this address?"));
        screen.PanelBlocks.Add(new Callout(T(s.Reason) + (s.Local ? "\n" + T("This address is on your own network. Blocking it may block one of your devices.") : ""),
            Tone.Warn, s.Address));
        screen.PanelBlocks.Add(new Para(T("Last seen") + " " + s.LastSeenUtc.ToLocalTime().ToString("g"), Tone.Muted));
        screen.Items.Add(Local(T("Yes"), () => yes = true) with { Tone = Tone.Warn });
        screen.Items.Add(Local(T("No"), () => { }));
        screen.Items.Add(Local(T("Dismiss this suggestion"), () => dismiss = true));
        var item = await ui.Menu(screen) ?? throw new Back();
        await item.Run!();
        if (yes) { await Write(nameof(Admin.BlockAddress), new { address = s.Address, reason = s.Reason }); Say(T("Blocked") + " " + s.Address); }
        else if (dismiss) await Write(nameof(Admin.DismissSuggestion), new { address = s.Address });
    }

    private async Task BlockedAddresses(NetworkOverview net)
    {
        string? pick = null;
        var screen = new Screen(T("Blocked addresses"));
        screen.Body.Add(new Para(net.BlockedAddresses.Length == 0 ? T("None.") : T("Pick an address to unblock it."), Tone.Muted));
        screen.Items.Add(Local(T("Block an address"), () => pick = "") with { Tone = Tone.Accent });
        foreach (var blocked in net.BlockedAddresses) screen.Items.Add(Local(blocked, () => pick = blocked));
        await (await ui.Menu(screen) ?? throw new Back()).Run!();
        if (pick == null) return;
        if (pick.Length == 0)
        {
            var address = await Ask("Address", help: "203.0.113.7 " + T("or") + " 203.0.113.0/24");
            var reason = await Ask("Reason (optional)");
            await Write(nameof(Admin.BlockAddress), new { address = address.Trim(), reason = reason.Trim() });
            return;
        }
        if (await YesNo("Unblock this address?", new Para(pick)))
            await Write(nameof(Admin.UnblockAddress), new { address = pick });
    }

    private async Task OutsideConnections()
    {
        var clients = await Read<RemoteClient[]>(nameof(Admin.GetRemoteClients));
        var screen = new Screen(T("Outside connections"));
        screen.Body.Add(new Para(T("Connections from outside your network since Melon started. Pick one to block it."), Tone.Muted));
        if (clients.Length == 0) screen.Body.Add(new Para(T("Nothing yet.")));
        else screen.Body.Add(new Table([T("Address"), T("User"), T("Requests"), T("Refused"), T("Last seen")], [false, false, true, true, false],
            clients.Select(c => (new[] { c.Address, c.Username ?? "", c.Requests.ToString("N0"), c.Denied.ToString("N0"), c.LastSeenUtc.ToLocalTime().ToString("g") },
                0d, c.Blocked ? Tone.Muted : c.Denied > 0 ? Tone.Warn : Tone.Normal))));
        foreach (var c in clients.Where(c => !c.Blocked))
            screen.Items.Add(new Entry(c.Address, async () =>
            {
                if (await YesNo("Block this address?", new Callout($"{c.Requests:N0} {T("requests")} · {c.Denied:N0} {T("refused")}", Tone.Warn, c.Address)))
                {
                    await Write(nameof(Admin.BlockAddress), new { address = c.Address, reason = "Blocked from the outside connections list" });
                    Say(T("Blocked") + " " + c.Address);
                }
            }) { Value = c.Username });
        var item = await ui.Menu(screen);
        if (item != null) await item.Run!();
    }

    // ----- Remote Access setup -----

    private async Task RemoteAccessWizard()
    {
        var net = await Read<NetworkOverview>(nameof(Admin.GetNetwork));
        await Confirm("Set up Remote Access", T(NetworkText.Warning));
        await ui.Busy(T("Checking your network…"));
        var d = await Read<NetworkDiagnostics>(nameof(Admin.DiagnoseNetwork));
        var current = net.Remote.Enabled && net.Remote.Method == "Automatic" ? net.Remote : null;

        bool Default(string option, bool? now)
        {
            var level = NetworkText.For(d, option)?.Level ?? "Optional";
            return level != "Unavailable" && (now ?? level is "Required" or "Recommended");
        }
        bool router = Default("RouterPort", current?.RouterPort), ipv6 = d.IPv6Works && Default("IPv6", current?.IPv6), pkarr = Default("Pkarr", current?.Pkarr);
        int port = current is { ExternalPort: > 0 } ? current.ExternalPort : net.HttpsPort;

        while (true)
        {
            string? next = null;
            var screen = new Screen(T("Remote Access"));
            screen.Body.Add(new Panel(T("What Melon found"), new KeyValues(NetworkText.Facts(d).Select(f => (T(f.Label), T(f.Value), f.Warn ? Tone.Warn : Tone.Normal)))));
            void Explain(string option, string question, string help)
            {
                var rec = NetworkText.For(d, option);
                var text = T(help) + (rec?.Reason is { Length: > 0 } reason ? "\n" + T(reason) : "");
                screen.Body.Add(new Panel(T(question), new Para(text), T(NetworkText.Level(rec?.Level ?? "Optional")), LevelTone(rec?.Level)));
            }
            Explain("RouterPort", NetworkText.RouterQuestion, NetworkText.RouterHelp);
            if (d.IPv6Works) Explain("IPv6", NetworkText.IPv6Question, NetworkText.IPv6Help);
            Explain("Pkarr", NetworkText.PkarrQuestion, NetworkText.PkarrHelp);

            Entry Toggle(string option, string question, bool on, string key)
            {
                var level = NetworkText.For(d, option)?.Level ?? "Optional";
                if (level == "Unavailable") return new Entry(T(question), () => Show(question, T(NetworkText.For(d, option)?.Reason ?? ""))) { Value = T("Not available"), ValueTone = Tone.Danger, Glyph = "○" };
                return Local(T(question), () => next = key) with { Value = T(on ? "Yes" : "No") + " · " + T(NetworkText.Level(level)), ValueTone = LevelTone(level), Glyph = on ? "●" : "○" };
            }
            screen.Items.Add(Toggle("RouterPort", NetworkText.RouterQuestion, router, "router"));
            if (router) screen.Items.Add(Local(T("Outside port"), () => next = "port") with { Value = port.ToString() });
            if (d.IPv6Works) screen.Items.Add(Toggle("IPv6", NetworkText.IPv6Question, ipv6, "ipv6"));
            screen.Items.Add(Toggle("Pkarr", NetworkText.PkarrQuestion, pkarr, "pkarr"));
            screen.Items.Add(Entry.Header(""));
            screen.Items.Add(Local(T("Turn on Remote Access"), () => next = "on") with { Tone = Tone.Accent });
            screen.Items.Add(Local(T("Advanced setup"), () => next = "advanced"));

            var item = await ui.Menu(screen);
            if (item == null) return;
            await item.Run!();
            switch (next)
            {
                case "router": router = !router; break;
                case "ipv6": ipv6 = !ipv6; break;
                case "pkarr": pkarr = !pkarr; break;
                case "port":
                    if (!int.TryParse(await Ask("Outside port", port.ToString(), help: T("The port opened on the router. Change it if another device already uses it.")), out var p) || p is < 1 or > 65535)
                        throw new ArgumentException(T("Enter a valid port."));
                    port = p; break;
                case "advanced":
                    if (await AdvancedSetup(net, d)) return;
                    break;
                case "on":
                    if (!router && !ipv6)
                    {
                        await Show("No way in", T("With the router port and IPv6 both off, devices outside your network have no way to reach Melon. Try the advanced setup instead, or turn one of them on."));
                        break;
                    }
                    await TurnOn(net, new RemoteAccessOptions("Automatic", router, ipv6, pkarr, router ? port : 0));
                    return;
            }
        }
    }

    private async Task TurnOn(NetworkOverview net, RemoteAccessOptions options)
    {
        await ui.Busy(T("Turning on Remote Access…"));
        var status = await Read<RemoteAccessStatus>(nameof(Admin.EnableRemoteAccess), new { options });
        if (options.Method == "Automatic" && await FirewallAllowed() == false &&
            await YesNo("Allow through Windows Firewall?", new Para(T(NetworkText.FirewallHelp))))
            try { await AllowFirewall(net); } catch (InvalidOperationException) { }
        Say(T("Remote Access") + ": " + T(NetworkText.State(status)) + (status.Detail is { } detail ? " · " + T(detail) : ""),
            status.State == "Problem" ? Tone.Warn : Tone.Good);
    }

    /// <summary>Reverse proxy, Tailscale, or a custom listening URL and certificate. Returns false to go back to the main options.</summary>
    private async Task<bool> AdvancedSetup(NetworkOverview net, NetworkDiagnostics d)
    {
        const string proxy = "Reverse proxy (Caddy, nginx, a domain you own)", tailscale = "Tailscale",
            custom = "Custom listening URL and certificate (advanced)", caddy = "How to set up Caddy", getTailscale = "Get Tailscale";
        var choices = new List<string> { proxy };
        if (net.TailscaleInstalled) choices.Add(tailscale);
        choices.Add(custom); choices.Add(caddy);
        if (!net.TailscaleInstalled) choices.Add(getTailscale);
        string pick;
        try { pick = await Choose("Advanced setup", choices); }
        catch (Back) { return false; }
        switch (pick)
        {
            case caddy: await Platform.Launch(NetworkText.CaddyGuide); return false;
            case getTailscale: await Platform.Launch(NetworkText.TailscaleDownload); return false;
            case custom: await CustomSetup(net); return true;
        }
        if (net.Mode != "Automatic")
        {
            await Confirm("Use automatic networking?", T("This needs automatic networking. Melon switches to it now and Remote Access starts after a restart."));
            await Write(nameof(Admin.UseAutomaticNetworking), new { httpPort = net.HttpPort, httpsPort = net.HttpsPort });
        }
        if (pick == tailscale)
        {
            if (d.TailscaleAddress == null) throw new InvalidOperationException(T("Tailscale is installed but not connected. Sign in to Tailscale first."));
            await TurnOn(net, new RemoteAccessOptions("Tailscale", false, false, false));
            return true;
        }
        var url = (await Ask("Public address", net.Remote.Method == "ReverseProxy" ? net.Remote.PublicUrl ?? "" : "",
            help: T($"Point your reverse proxy at https://localhost:{net.HttpsPort} (or http://localhost:{net.HttpPort}). Melon only accepts outside connections through it."))).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException(T("Enter the public address, like https://music.example.com."));
        await TurnOn(net, new RemoteAccessOptions("ReverseProxy", false, false, false, 0, url));
        return true;
    }

    /// <summary>The old listening URL + certificate setup. Switches Melon to custom networking.</summary>
    private async Task CustomSetup(NetworkOverview net)
    {
        await Confirm("Custom setup", T("Melon stops managing its own ports and certificate, and the local network broadcast and automatic Remote Access turn off. Separate several URLs with ;. Melon needs a restart after this."));
        var listen = (await Ask("Listening URL", net.Mode == "Automatic" ? $"https://*:{net.HttpsPort}" : net.ListeningUrl, help: "https://*:443")).Trim();
        if (listen.Length == 0) throw new ArgumentException(T("Enter a listening URL."));
        string path = "", password = "";
        if (listen.Contains("https:", StringComparison.OrdinalIgnoreCase))
        {
            path = await ui.PickPath(T("Certificate (.pfx)"), false, net.CertificatePath ?? "", ".pfx", ".p12");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(T("Certificate (.pfx)"));
            path = Path.GetFullPath(path);
            password = await Ask("Certificate password", secret: true);
            if (path == net.CertificatePath && password.Length == 0) path = "";
        }
        await Write(nameof(Admin.UseCustomNetworking), new { listeningUrl = listen, certificatePath = path, certificatePassword = password });
        firewallAllowed = null;
        Say(T("Custom setup saved."));
    }

    // ----- After first-time setup -----

    private async Task NetworkIntro(Screen page)
    {
        var net = await Read<NetworkOverview>(nameof(Admin.GetNetwork));
        page.ActiveSection = page.Title;
        page.Body.Add(new Callout(T(NetworkText.IntroHome), Tone.Good, T("At home")));
        page.Body.Add(new Callout(T(NetworkText.IntroAway), Tone.Info, T("Away from home")));
        var firewall = await FirewallAllowed();
        if (firewall == false) page.Body.Add(new Callout(T(NetworkText.FirewallHelp), Tone.Warn, T("Windows Firewall may block other devices")));
        page.Items.Add(Action("Set up Remote Access", async () => { networkIntro = false; section = "Networking"; await RemoteAccessWizard(); }) with { Tone = Tone.Accent });
        if (firewall == false) page.Items.Add(Action("Allow through Windows Firewall", () => AllowFirewall(net)));
        page.Items.Add(Local(T("Skip for now"), () => networkIntro = false));
    }
}
