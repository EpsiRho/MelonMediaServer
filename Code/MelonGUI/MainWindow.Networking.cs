using Melon.Controllers;
using Melon.Interface;
using Melon.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private bool networkIntro;
    private bool? firewallAllowed;

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

    private static TextBlock Caption(string value) => Text(value, "CaptionTextBlockStyle");

    private static StackPanel Across(params UIElement[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        foreach (var item in items) panel.Children.Add(item);
        return panel;
    }

    /// <summary>A small coloured tag: Required, Recommended, Optional or Not available.</summary>
    private Border Badge(string level) => new()
    {
        CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 2), VerticalAlignment = VerticalAlignment.Center,
        Background = Resource(level switch
        {
            "Required" => "SystemFillColorCautionBackgroundBrush",
            "Recommended" => "SystemFillColorSuccessBackgroundBrush",
            "Unavailable" => "SystemFillColorCriticalBackgroundBrush",
            _ => "SystemFillColorNeutralBackgroundBrush",
        }),
        Child = Caption(T(NetworkText.Level(level))),
    };

    private HyperlinkButton Link(string label, string url)
    {
        var link = new HyperlinkButton { Content = T(label), Padding = new Thickness(0) };
        link.Click += (_, _) => { try { DesktopHost.OpenLink(url); } catch (Exception ex) { Error(ex); } };
        return link;
    }

    private static int[] Ports(NetworkOverview net)
    {
        if (net.Mode == "Automatic") return [net.HttpPort, net.HttpsPort];
        return net.ListeningUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => Uri.TryCreate(u.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:"), UriKind.Absolute, out var uri) ? uri.Port : 0)
            .Where(p => p > 0).Distinct().ToArray();
    }

    private async Task<bool> FirewallAllowed()
    {
        if (firewallAllowed is bool known) return known;
        try { firewallAllowed = await DesktopHost.FirewallRuleExists(lifetime.Token); }
        catch (Exception ex) when (ex is not OperationCanceledException) { firewallAllowed = true; } // can't tell: don't nag
        return firewallAllowed.Value;
    }

    private async Task AllowFirewall(int[] ports)
    {
        try { await DesktopHost.AllowThroughFirewall(ports, lifetime.Token); }
        catch (System.ComponentModel.Win32Exception) { throw new OperationCanceledException(T("Windows Firewall wasn't changed.")); }
        firewallAllowed = true;
        Notice("Melon is allowed through Windows Firewall.");
    }

    private async Task<UIElement> FirewallCard(NetworkOverview net)
    {
        bool allowed = await FirewallAllowed();
        var title = Text(T(allowed ? "Allowed through Windows Firewall" : "Windows Firewall may block other devices"), "BodyStrongTextBlockStyle");
        if (!allowed) title.Foreground = Resource("SystemFillColorCautionBrush");
        return Card(Row(Stack(title, Caption(T(NetworkText.FirewallHelp))),
            ButtonFor(allowed ? "Allow again" : "Allow through Windows Firewall", () => AllowFirewall(Ports(net)), !allowed, refresh: true)));
    }

    private async Task NetworkingPage(StackPanel page)
    {
        var net = await Read<NetworkOverview>(nameof(IMelonAdministration.GetNetwork));
        bool automatic = net.Mode == "Automatic";

        // How Melon listens.
        Section(page, "Connection");
        if (automatic)
        {
            page.Children.Add(Card(Stack(
                Row(Stack(Text(T("Automatic"), "BodyStrongTextBlockStyle"),
                        Text(T("Melon listens on every network this computer is on and makes its own HTTPS certificate. Apps check that certificate by its fingerprint, so you don't need a domain name."))),
                    ButtonFor("Change ports", () => EditPorts(net), refresh: true)),
                Caption($"HTTP {net.HttpPort} · HTTPS {net.HttpsPort}"),
                Across(ButtonFor("Server QR code", () => ShowServerQr(net), true)),
                AddressList(net))));
        }
        else
        {
            var details = Stack(Caption(T("Listening URL") + ": " + net.ListeningUrl));
            if (!string.IsNullOrEmpty(net.CertificatePath)) details.Children.Add(Caption(T("Certificate") + ": " + net.CertificatePath));
            page.Children.Add(Card(Stack(
                Row(Stack(Text(T("Custom"), "BodyStrongTextBlockStyle"),
                        Text(T("Melon uses your own listening URL and certificate. Apps can still connect by that address, but the local network broadcast and automatic Remote Access need automatic networking."))),
                    ButtonFor("Use automatic networking", async () =>
                    {
                        if (await Dialog("Use automatic networking?", Text(T("Melon will listen on ports 14524 (HTTP) and 14525 (HTTPS) with its own certificate. Your custom listening URL and certificate stop being used. Melon needs a restart after this.")), "Switch"))
                            await Command(nameof(IMelonAdministration.UseAutomaticNetworking), new { httpPort = 14524, httpsPort = 14525 });
                    }, true)),
                details, Across(ButtonFor("Edit custom setup", () => CustomSetup(net), refresh: true), ButtonFor("Server QR code", () => ShowServerQr(net))),
                AddressList(net))));
        }
        page.Children.Add(await FirewallCard(net));

        // Local network broadcast.
        Section(page, "Local Network Broadcast");
        var broadcast = new ToggleSwitch { Header = T("Let devices on this network find Melon"), IsOn = net.Broadcast };
        var name = new TextBox { Header = T("Name"), Text = net.BroadcastName, PlaceholderText = "Melon on " + Environment.MachineName, MaxLength = 63 };
        var type = new TextBox { Header = T("Service type"), Text = net.ServiceType, IsReadOnly = true };
        ToolTipService.SetToolTip(type, T("Apps look for this type. It's the same for every Melon server."));
        var broadcastCard = Stack(broadcast, Caption(T(NetworkText.BroadcastHelp)), name, type,
            Across(ButtonFor("Save", async () =>
            {
                await Command(nameof(IMelonAdministration.SetBroadcast), new { enabled = broadcast.IsOn, name = name.Text.Trim() });
                Notice("Broadcast settings saved.");
            })));
        if (net.Broadcast && net.BroadcastProblem == null) broadcastCard.Children.Insert(2, Caption(T("Broadcasting as") + " \"" + net.BroadcastNameInUse + "\""));
        if (net.BroadcastProblem != null)
        {
            var problem = Text(T(net.BroadcastProblem)); problem.Foreground = Resource("SystemFillColorCautionBrush");
            broadcastCard.Children.Insert(2, problem);
        }
        page.Children.Add(Card(broadcastCard));

        // Remote Access.
        Section(page, "Remote Access");
        page.Children.Add(RemoteAccessCard(net));

        await SecuritySection(page, net);
    }

    /// <summary>Every address apps can use, the certificate fingerprint and the Pkarr key, folded away: apps learn them from QR codes.</summary>
    private UIElement AddressList(NetworkOverview net)
    {
        var list = Stack(Caption(T("Apps learn these from QR codes and keep them up to date, so nobody has to type them.")));
        foreach (var endpoint in net.Endpoints)
        {
            var url = Text(endpoint.Url); url.IsTextSelectionEnabled = true;
            list.Children.Add(Row(url, Caption(T(NetworkText.Kind(endpoint.Kind)))));
        }
        if (net.Fingerprint != null) { var fp = Caption(T("Certificate fingerprint") + ": " + net.Fingerprint); fp.IsTextSelectionEnabled = true; list.Children.Add(fp); }
        if (net.PkarrKey != null) { var key = Caption("Pkarr " + T("key") + ": " + net.PkarrKey); key.IsTextSelectionEnabled = true; list.Children.Add(key); }
        return new Expander { Header = T("Addresses") + $" ({net.Endpoints.Length})", Content = list,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    }

    /// <summary>A QR code with just the server: apps fill in the address and how to find it, then you sign in as usual.</summary>
    private async Task ShowServerQr(NetworkOverview net)
    {
        var link = net.ServerLink ?? net.Endpoints.FirstOrDefault()?.Url;
        if (link == null) { Notice("Melon doesn't have an address other devices can use yet."); return; }
        var url = Caption(link); url.IsTextSelectionEnabled = true; url.MaxWidth = 340;
        if (await Dialog("Server QR code", Stack(await QrImage(link), Text(T(NetworkText.ServerQrHelp)), url), "Copy link", cancel: "Close"))
        {
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(link);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            Notice("Copied");
        }
    }
    private UIElement RemoteAccessCard(NetworkOverview net)
    {
        var r = net.Remote;
        if (net.Mode != "Automatic")
            return Card(Row(Stack(Text(T("Custom setup"), "BodyStrongTextBlockStyle"),
                    Text(T("Your listening URL decides who can reach Melon. To let Melon handle Remote Access for you, switch to automatic networking."))),
                ButtonFor("Set up Remote Access", RemoteAccessWizard, refresh: true)));
        if (!r.Enabled)
            return Card(Row(Stack(Text(T("Off"), "BodyStrongTextBlockStyle"),
                    Text(T("Only devices on your network can connect. Set up Remote Access to listen away from home."))),
                ButtonFor("Set up Remote Access", RemoteAccessWizard, true, refresh: true)));
        var state = Text(T(NetworkText.State(r)), "BodyStrongTextBlockStyle");
        if (r.State == "Problem") state.Foreground = Resource("SystemFillColorCautionBrush");
        var body = Stack(state, Caption(T(NetworkText.Line(r))));
        if (r.Detail != null) body.Children.Add(Text(T(r.Detail)));
        body.Children.Add(Caption(T(NetworkText.LastOutside(r))));
        body.Children.Add(Across(
            ButtonFor("Change", RemoteAccessWizard, refresh: true),
            ButtonFor("Turn off", async () =>
            {
                if (await Dialog("Turn off Remote Access?", Text(T("Devices away from home won't be able to connect until you turn it on again. Melon closes the router port it opened.")), "Turn off", true))
                    await Command(nameof(IMelonAdministration.DisableRemoteAccess));
            })));
        return Card(body);
    }

    private async Task SecuritySection(StackPanel page, NetworkOverview net)
    {
        Section(page, "Security");
        SecuritySuggestion[] suggestions = [];
        try { suggestions = await Read<SecuritySuggestion[]>(nameof(IMelonAdministration.GetSecuritySuggestions)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        if (suggestions.Length == 0)
            page.Children.Add(Card(Text(T(net.Remote.Enabled ? "Nothing looks suspicious right now." : "Nothing looks suspicious. While Remote Access is on, Melon keeps an eye on outside connections and suggests addresses to block here."))));
        foreach (var s in suggestions)
        {
            var title = Text(s.Address + (s.Local ? " · " + T("on your network") : ""), "BodyStrongTextBlockStyle");
            title.IsTextSelectionEnabled = true;
            page.Children.Add(Card(Row(
                Stack(title, Text(T(s.Reason)), Caption(T("Last seen") + " " + s.LastSeenUtc.ToLocalTime().ToString("g"))),
                Across(
                    ButtonFor("Block", async () =>
                    {
                        var why = Stack(Text(s.Address, "BodyStrongTextBlockStyle"), Text(T(s.Reason)));
                        if (s.Local) why.Children.Add(Caption(T("This address is on your own network. Blocking it may block one of your devices.")));
                        if (await Dialog("Block this address?", why, "Yes", false, "No"))
                            await Command(nameof(IMelonAdministration.BlockAddress), new { address = s.Address, reason = s.Reason });
                    }, true),
                    ButtonFor("Dismiss", () => Command(nameof(IMelonAdministration.DismissSuggestion), new { address = s.Address }))))));
        }

        var blocked = Stack(Text(T("Blocked addresses"), "BodyStrongTextBlockStyle"));
        if (net.BlockedAddresses.Length == 0) blocked.Children.Add(Caption(T("None.")));
        foreach (var address in net.BlockedAddresses)
            blocked.Children.Add(Row(Text(address), ButtonFor("Unblock", () => Command(nameof(IMelonAdministration.UnblockAddress), new { address }))));
        blocked.Children.Add(Across(
            ButtonFor("Block an address", BlockManually),
            ButtonFor("Outside connections", ShowRemoteClients, refresh: false),
            ButtonFor("Security events", async () =>
                await ShowSecurityEvents(await Read<SecurityEvent[]>(nameof(IMelonAdministration.GetSecurityEvents), new { count = 300, warningsOnly = false })), refresh: false)));
        page.Children.Add(Card(blocked));
    }

    private async Task BlockManually()
    {
        var address = new TextBox { Header = T("Address"), PlaceholderText = "203.0.113.7 or 203.0.113.0/24" };
        var reason = new TextBox { Header = T("Reason (optional)") };
        if (!await Dialog("Block an address", Stack(address, reason), "Block")) return;
        await Command(nameof(IMelonAdministration.BlockAddress), new { address = address.Text.Trim(), reason = reason.Text.Trim() });
    }

    /// <summary>Who reached Melon from outside the home network since it started, with a Block button that asks first.</summary>
    private async Task ShowRemoteClients()
    {
        var clients = await Read<RemoteClient[]>(nameof(IMelonAdministration.GetRemoteClients));
        var list = new StackPanel { Spacing = 10, MinWidth = 460 };
        list.Children.Add(Caption(T("Connections from outside your network since Melon started.")));
        if (clients.Length == 0) list.Children.Add(Text(T("Nothing yet.")));
        foreach (var c in clients)
        {
            var who = Text(c.Address + (c.Username != null ? " · " + c.Username : ""), "BodyStrongTextBlockStyle");
            who.IsTextSelectionEnabled = true;
            var info = Caption($"{c.Requests:N0} {T("requests")} · {c.Denied:N0} {T("refused")} · {T("last")} {c.LastSeenUtc.ToLocalTime():g}");
            UIElement action;
            if (c.Blocked) action = Caption(T("Blocked"));
            else
            {
                var block = new Button { Content = T("Block") };
                var yes = new Button { Content = T("Yes"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                var no = new Button { Content = T("No") };
                var flyout = new Flyout { Content = Stack(Text(T("Block this address?")), Across(yes, no)) };
                block.Flyout = flyout;
                no.Click += (_, _) => flyout.Hide();
                yes.Click += async (_, _) =>
                {
                    flyout.Hide();
                    try
                    {
                        await Command(nameof(IMelonAdministration.BlockAddress), new { address = c.Address, reason = "Blocked from the outside connections list" });
                        block.Content = T("Blocked"); block.IsEnabled = false;
                    }
                    catch (Exception ex) { Error(ex); }
                };
                action = block;
            }
            list.Children.Add(Row(Stack(who, info), (FrameworkElement)action));
        }
        await new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T("Outside connections"),
            Content = new ScrollViewer { Content = list, MaxHeight = 520 }, CloseButtonText = T("Close") }.ShowAsync();
        await Render();
    }

    private async Task EditPorts(NetworkOverview net)
    {
        var http = new NumberBox { Header = T("HTTP port (home network)"), Minimum = 1, Maximum = 65535, Value = net.HttpPort };
        var https = new NumberBox { Header = T("HTTPS port"), Minimum = 1, Maximum = 65535, Value = net.HttpsPort };
        if (!await Dialog("Ports", Stack(http, https, Caption(T("Melon needs a restart after changing ports. Apps find the new ports by themselves while they're on your network."))), "Save")) return;
        if (double.IsNaN(http.Value) || double.IsNaN(https.Value)) throw new ArgumentException(T("Enter a valid port."));
        await Command(nameof(IMelonAdministration.UseAutomaticNetworking), new { httpPort = (int)http.Value, httpsPort = (int)https.Value });
        firewallAllowed = null;
    }

    // ----- Remote Access setup -----

    private async Task<TValue> WithProgress<TValue>(string title, string detail, Func<Task<TValue>> work)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T(title),
            Content = Stack(new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Left }, Text(T(detail))) };
        var shown = dialog.ShowAsync();
        try { return await work(); }
        finally { dialog.Hide(); try { await shown; } catch (Exception) { } }
    }

    private async Task RemoteAccessWizard()
    {
        var net = await Read<NetworkOverview>(nameof(IMelonAdministration.GetNetwork));
        if (!await Dialog("Set up Remote Access", Text(T(NetworkText.Warning)), "Continue")) return;
        var d = await WithProgress("Checking your network…", "Melon is looking up your public address and asking your router what it supports. This can take half a minute.",
            () => Read<NetworkDiagnostics>(nameof(IMelonAdministration.DiagnoseNetwork)));

        while (true)
        {
            var choice = await AutomaticOptions(net, d);
            if (choice == null) return;
            if (choice is RemoteAccessOptions options) { await TurnOn(net, options); return; }
            // "Advanced setup"
            var done = await AdvancedSetup(net, d);
            if (done) return;
        }
    }

    /// <summary>The main options. Returns the options, "advanced" for the advanced setup, or null when cancelled.</summary>
    private async Task<object?> AutomaticOptions(NetworkOverview net, NetworkDiagnostics d)
    {
        var current = net.Remote.Enabled && net.Remote.Method == "Automatic" ? net.Remote : null;
        var content = new StackPanel { Spacing = 12, MinWidth = 480 };

        var facts = new Grid { ColumnSpacing = 16, RowSpacing = 2 };
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var (label, value, warn) in NetworkText.Facts(d))
        {
            var k = Caption(T(label)); var v = Caption(T(value));
            if (warn) v.Foreground = Resource("SystemFillColorCautionBrush");
            Grid.SetRow(k, facts.RowDefinitions.Count); Grid.SetRow(v, facts.RowDefinitions.Count); Grid.SetColumn(v, 1);
            facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            facts.Children.Add(k); facts.Children.Add(v);
        }
        content.Children.Add(Stack(Text(T("What Melon found"), "BodyStrongTextBlockStyle"), facts));

        CheckBox Option(string key, string question, string help, bool? now, out Border badge)
        {
            var rec = NetworkText.For(d, key);
            var level = rec?.Level ?? "Optional";
            var box = new CheckBox { Content = T(question), IsEnabled = level != "Unavailable",
                IsChecked = level != "Unavailable" && (now ?? level is "Required" or "Recommended") };
            ToolTipService.SetToolTip(box, T(help));
            badge = Badge(level);
            var info = new FontIcon { Glyph = "", FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            ToolTipService.SetToolTip(info, T(help));
            // What the option does is in the tooltip; under it, only why it's recommended (or not) on this network.
            var block = Stack(Across(box, badge, info));
            if (rec?.Reason is { Length: > 0 } reason) block.Children.Add(Caption(T(reason)));
            content.Children.Add(block);
            return box;
        }

        var router = Option("RouterPort", NetworkText.RouterQuestion, NetworkText.RouterHelp, current?.RouterPort, out _);
        var port = new NumberBox { Header = T("Outside port"), Minimum = 1, Maximum = 65535, Value = current is { ExternalPort: > 0 } ? current.ExternalPort : net.HttpsPort,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 160 };
        ToolTipService.SetToolTip(port, T("The port opened on the router. Change it if another device already uses it."));
        content.Children.Add(port);
        router.Checked += (_, _) => port.IsEnabled = true;
        router.Unchecked += (_, _) => port.IsEnabled = false;
        port.IsEnabled = router.IsChecked == true;

        CheckBox? ipv6 = d.IPv6Works ? Option("IPv6", NetworkText.IPv6Question, NetworkText.IPv6Help, current?.IPv6, out _) : null;
        var pkarr = Option("Pkarr", NetworkText.PkarrQuestion, NetworkText.PkarrHelp, current?.Pkarr, out _);

        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T("Remote Access"),
            Content = new ScrollViewer { Content = content, MaxHeight = 560 }, PrimaryButtonText = T("Turn on"),
            SecondaryButtonText = T("Advanced setup"), CloseButtonText = T("Cancel"), DefaultButton = ContentDialogButton.Primary };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary) return "advanced";
        if (result != ContentDialogResult.Primary) return null;
        bool useRouter = router.IsChecked == true, useV6 = ipv6?.IsChecked == true;
        if (!useRouter && !useV6)
        {
            if (!await Dialog("No way in", Text(T("With the router port and IPv6 both off, devices outside your network have no way to reach Melon. Try the advanced setup instead, or turn one of them on.")), "Go back")) return null;
            return await AutomaticOptions(net, d);
        }
        if (double.IsNaN(port.Value)) throw new ArgumentException(T("Enter a valid port."));
        return new RemoteAccessOptions("Automatic", useRouter, useV6, pkarr.IsChecked == true, useRouter ? (int)port.Value : 0);
    }

    private async Task TurnOn(NetworkOverview net, RemoteAccessOptions options)
    {
        var status = await WithProgress("Turning on Remote Access…", "Opening the router port and publishing Melon's address.",
            () => Read<RemoteAccessStatus>(nameof(IMelonAdministration.EnableRemoteAccess), new { options }));
        if (options.Method == "Automatic" && !await FirewallAllowed())
        {
            if (await Dialog("Allow through Windows Firewall?", Text(T(NetworkText.FirewallHelp)), "Allow"))
                try { await AllowFirewall(Ports(net)); } catch (OperationCanceledException) { }
        }
        Notice(T("Remote Access") + ": " + T(NetworkText.State(status)) + (status.Detail is { } detail ? " · " + T(detail) : ""),
            status.State == "Problem" ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
    }

    /// <summary>Reverse proxy, Tailscale, or a custom listening URL and certificate. Returns false to go back to the main options.</summary>
    private async Task<bool> AdvancedSetup(NetworkOverview net, NetworkDiagnostics d)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 480 };
        content.Children.Add(Text(T("For people who already run their own setup. Most people don't need these.")));

        var proxy = new RadioButton { Content = T("Reverse proxy (Caddy, nginx, a domain you own)"), GroupName = "advanced", IsChecked = true };
        var publicUrl = new TextBox { Header = T("Public address"), PlaceholderText = "https://music.example.com",
            Text = net.Remote.Method == "ReverseProxy" ? net.Remote.PublicUrl ?? "" : "" };
        var proxyPanel = Stack(Caption(T($"Point your reverse proxy at https://localhost:{net.HttpsPort} (or http://localhost:{net.HttpPort}). Melon only accepts outside connections through it.")),
            publicUrl, Link("How to set up Caddy", NetworkText.CaddyGuide));
        content.Children.Add(Stack(proxy, proxyPanel));

        RadioButton? tailscale = null;
        var tailscaleRec = NetworkText.For(d, "Tailscale");
        if (net.TailscaleInstalled)
        {
            tailscale = new RadioButton { Content = T("Tailscale"), GroupName = "advanced", IsChecked = net.Remote.Method == "Tailscale" };
            var detail = Stack(Caption(T(d.TailscaleAddress != null
                ? "Devices signed in to your Tailscale network reach Melon at " + (d.TailscaleName ?? d.TailscaleAddress) + ". Nothing is opened to the rest of the internet."
                : "Tailscale is installed but not connected. Sign in to Tailscale first.")));
            if (tailscaleRec?.Reason is { Length: > 0 } reason) detail.Children.Add(Caption(T(reason)));
            content.Children.Add(Stack(Across(tailscale, Badge(tailscaleRec?.Level ?? "Optional")), detail));
        }
        else content.Children.Add(Stack(Caption(T("Tailscale isn't installed on this computer. It's a free way to reach Melon from your own devices without opening anything to the internet.")),
            Link("Get Tailscale", NetworkText.TailscaleDownload)));

        var custom = new RadioButton { Content = T("Custom listening URL and certificate (advanced)"), GroupName = "advanced", IsChecked = net.Mode != "Automatic" };
        var (customForm, applyCustom) = CustomForm(net);
        content.Children.Add(Stack(custom, customForm));
        if (net.Remote.Method == "Tailscale" && tailscale != null) tailscale.IsChecked = true;

        void Show()
        {
            proxyPanel.Visibility = proxy.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            customForm.Visibility = custom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        foreach (var radio in new[] { proxy, tailscale, custom }) if (radio != null) { radio.Checked += (_, _) => Show(); }
        Show();

        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T("Advanced setup"),
            Content = new ScrollViewer { Content = content, MaxHeight = 560 }, PrimaryButtonText = T("Apply"),
            SecondaryButtonText = T("Back"), CloseButtonText = T("Cancel"), DefaultButton = ContentDialogButton.Primary };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary) return false;
        if (result != ContentDialogResult.Primary) return true;

        if (custom.IsChecked == true) { await applyCustom(); return true; }
        if (net.Mode != "Automatic")
        {
            if (!await Dialog("Use automatic networking?", Text(T("This needs automatic networking. Melon switches to it now and Remote Access starts after a restart.")), "Switch")) return true;
            await Command(nameof(IMelonAdministration.UseAutomaticNetworking), new { httpPort = net.HttpPort, httpsPort = net.HttpsPort });
        }
        if (tailscale?.IsChecked == true) await TurnOn(net, new RemoteAccessOptions("Tailscale", false, false, false));
        else
        {
            var url = publicUrl.Text.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new ArgumentException(T("Enter the public address, like https://music.example.com."));
            await TurnOn(net, new RemoteAccessOptions("ReverseProxy", false, false, false, 0, url));
        }
        return true;
    }

    /// <summary>The old listening URL + certificate form. Applying it switches Melon to custom networking.</summary>
    private (StackPanel Form, Func<Task> Apply) CustomForm(NetworkOverview net)
    {
        var url = new TextBox { Header = T("Listening URL"), Text = net.Mode == "Automatic" ? $"https://*:{net.HttpsPort}" : net.ListeningUrl };
        var cert = new TextBox { Header = T("Certificate (.pfx)"), Text = net.CertificatePath ?? "" };
        var browse = new Button { Content = T("Browse"), VerticalAlignment = VerticalAlignment.Bottom };
        browse.Click += async (_, _) => { try { if (await PickFile(".pfx", ".p12") is string file) cert.Text = file; } catch (Exception ex) { Error(ex); } };
        var password = new PasswordBox { Header = T("Certificate password") };
        var form = Stack(Caption(T("Melon stops managing its own ports and certificate, and the local network broadcast and automatic Remote Access turn off. Separate several URLs with ;. Melon needs a restart after this.")),
            url, Row(cert, browse), password);
        async Task Apply()
        {
            var listen = url.Text.Trim();
            if (listen.Length == 0) throw new ArgumentException(T("Enter a listening URL."));
            bool keepCert = cert.Text.Trim() == (net.CertificatePath ?? "") && password.Password.Length == 0;
            await Command(nameof(IMelonAdministration.UseCustomNetworking), new { listeningUrl = listen,
                certificatePath = keepCert ? "" : cert.Text.Trim(), certificatePassword = password.Password });
            firewallAllowed = null;
            Notice("Custom setup saved.");
        }
        return (form, Apply);
    }

    private async Task CustomSetup(NetworkOverview net)
    {
        var (form, apply) = CustomForm(net);
        if (await Dialog("Custom setup", form, "Save")) await apply();
    }

    /// <summary>Where an invite will work from, for the invite dialog. Null when it can't be told.</summary>
    private async Task<string?> InviteReach()
    {
        try
        {
            var net = await Read<NetworkOverview>(nameof(IMelonAdministration.GetNetwork));
            return NetworkText.InviteReach(net);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    // ----- After first-time setup -----

    private async Task NetworkIntroPage(StackPanel page)
    {
        var net = await Read<NetworkOverview>(nameof(IMelonAdministration.GetNetwork));
        page.Children.Add(Card(Stack(
            Text(T("At home"), "BodyStrongTextBlockStyle"), Text(T(NetworkText.IntroHome)),
            Text(T("Away from home"), "BodyStrongTextBlockStyle"), Text(T(NetworkText.IntroAway)))));
        page.Children.Add(await FirewallCard(net));
        void Done() { networkIntro = false; BuildNavigation(); }
        page.Children.Add(Across(
            ButtonFor("Set up Remote Access", async () => { await RemoteAccessWizard(); Done(); tab = "Networking"; BuildNavigation(); }, true),
            ButtonFor("Skip for now", () => { Done(); return Task.CompletedTask; })));
    }
}
