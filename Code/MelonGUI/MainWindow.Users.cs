using System.Diagnostics;
using Melon.Controllers;
using Melon.Interface;
using Melon.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Net.Codecrete.QrCodeGenerator;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace MelonGUI;

public sealed partial class MainWindow
{
    /// <summary>Sign-ins, failures and account changes, newest first (Melon keeps 90 days).</summary>
    private async Task ShowSecurityEvents(SecurityEvent[] events)
    {
        var list = new StackPanel { Spacing = 10, MinWidth = 460 };
        list.Children.Add(Text(T("Sign-ins, failed attempts, signed-out devices and account changes. Kept for 90 days."), "CaptionTextBlockStyle"));
        if (events.Length == 0) list.Children.Add(Text(T("Nothing recorded yet.")));
        foreach (var e in events)
        {
            var title = Text(T(SecurityEventText.Label(e.Kind)) + (string.IsNullOrEmpty(e.Username) ? "" : "  ·  " + e.Username), "BodyStrongTextBlockStyle");
            if (e.Warning) title.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
            var where = SecurityEventText.Where(e);
            list.Children.Add(Stack(title, Text(e.TimeUtc.ToLocalTime().ToString("g") + (where.Length > 0 ? "  ·  " + where : ""), "CaptionTextBlockStyle")));
        }
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T("Security events"),
            Content = new ScrollViewer { Content = list, MaxHeight = 520 }, CloseButtonText = T("Close") };
        await dialog.ShowAsync();
    }

    private StackPanel? inviteRows;
    private AccessCode[] activeInvites = [];
    private string InviteTime(DateTime expires)
    {
        var remaining = expires.ToUniversalTime() - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) return T("Expired");
        return expires.ToLocalTime().ToString("t") + " · " +
            (remaining.TotalHours >= 1 ? $"{Math.Ceiling(remaining.TotalHours):N0} h" :
             remaining.TotalMinutes >= 1 ? $"{Math.Ceiling(remaining.TotalMinutes):N0} min" : $"{Math.Ceiling(remaining.TotalSeconds):N0} s");
    }

    private async Task UsersPage(StackPanel page)
    {
        var users = status!.DatabaseConnected ? await Read<ManagedUser[]>(nameof(IMelonAdministration.GetUsers)) : [];
        if (status.DatabaseConnected)
        {
            foreach (var user in users)
            {
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                actions.Children.Add(IconAction("\uE70F", T("Edit user type") + " · " + user.Username, () => EditUserType(user)));
                actions.Children.Add(ButtonFor("Reset code", async () =>
                    await ShowCode(await Read<AccessCode>(nameof(IMelonAdministration.CreatePasswordReset), new { userId = user.Id }), user.Username)));
                actions.Children.Add(IconAction("\uE74D", T("Delete user") + " " + user.Username, async () =>
                {
                    if (await Dialog("Delete user?", Stack(Text(user.Username), Text(T("Listening stats are kept. You can prune them separately in Stats."))), "Delete", true))
                        await Command(nameof(IMelonAdministration.DeleteUser), new { userId = user.Id });
                }));
                page.Children.Add(Card(Row(Stack(Text(user.Username, "BodyStrongTextBlockStyle"), Text(T(user.Role), "CaptionTextBlockStyle")), actions)));
            }
        }
        Section(page, "Invites");
        page.Children.Add(ButtonFor("Create invite", async () =>
        {
            var minutes = new NumberBox { Header = T("Expires in (minutes)"), Value = 10, Minimum = 1, Maximum = int.MaxValue,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            if (!await Dialog("Create invite", minutes, "Create")) return;
            if (!double.IsFinite(minutes.Value) || minutes.Value != Math.Truncate(minutes.Value) || minutes.Value < 1)
                throw new ArgumentException(T("Enter a positive number of minutes."));
            await ShowCode(await Read<AccessCode>(nameof(IMelonAdministration.CreateInvite), new { minutes = checked((int)minutes.Value) }), null);
        }, true));
        var rows = Stack();
        var invites = await Read<AccessCode[]>(nameof(IMelonAdministration.GetInvites));
        inviteRows = rows;
        activeInvites = invites;
        DrawInvites();
        page.Children.Add(rows);

        // Security settings live here, not in the web API, so a stolen admin token can't change them.
        var config = settings!;
        Section(page, "Security");
        var art = new NumberBox { Header = T("Artwork upload limit (MB)"), Value = config.ArtUploadMegabytes, Minimum = 1, Maximum = 200,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var lists = new NumberBox { Header = T("Playlist import limit (MB)"), Value = config.PlaylistImportMegabytes, Minimum = 1, Maximum = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        page.Children.Add(Card(Row(Stack(art, lists), ButtonFor("Save", async () =>
        {
            if (!double.IsFinite(art.Value) || !double.IsFinite(lists.Value)) throw new ArgumentException(T("Enter a whole number."));
            await Command(nameof(IMelonAdministration.SetUploadLimits), new { artMegabytes = (int)art.Value, playlistMegabytes = (int)lists.Value });
        }))));
        var logSize = new NumberBox { Header = T("Log file size (MB)"), Value = config.LogFileMegabytes, Minimum = 1, Maximum = 1024,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var logKept = new NumberBox { Header = T("Log files kept"), Value = config.LogFilesKept, Minimum = 1, Maximum = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        page.Children.Add(Card(Row(Stack(logSize, logKept, Text(T("Older files are deleted. Applies after a restart."), "CaptionTextBlockStyle")), ButtonFor("Save", async () =>
        {
            if (!double.IsFinite(logSize.Value) || !double.IsFinite(logKept.Value)) throw new ArgumentException(T("Enter a whole number."));
            await Command(nameof(IMelonAdministration.SetLogLimits), new { fileMegabytes = (int)logSize.Value, filesKept = (int)logKept.Value });
        }))));
        if (status!.DatabaseConnected)
        {
            var recent = await Read<SecurityEvent[]>(nameof(IMelonAdministration.GetSecurityEvents), new { count = 200, warningsOnly = false });
            int warnings = recent.Count(e => e.Warning && e.TimeUtc > DateTime.UtcNow.AddDays(-1));
            page.Children.Add(Card(Row(Stack(Text(T("Security events"), "BodyStrongTextBlockStyle"),
                Text(warnings > 0 ? $"{warnings:N0} " + T("warnings in 24 h") : T("No warnings in 24 h"), "CaptionTextBlockStyle")),
                ButtonFor("View", () => ShowSecurityEvents(recent), refresh: false))));
        }
        page.Children.Add(Card(Row(Stack(Text(T("Sign out every device"), "BodyStrongTextBlockStyle"),
            Text(T("Every app on every device has to sign in again."), "CaptionTextBlockStyle")),
            ButtonFor("Sign out every device", async () =>
            {
                if (await Dialog("Sign out every device?", Text(T("Every app on every device has to sign in again.")), "Sign out", true))
                    await Command(nameof(IMelonAdministration.SignOutEveryone));
            }))));
    }

    private async Task EditUserType(ManagedUser user)
    {
        var type = Choices("Account type", "User", "Admin");
        type.SelectedItem = type.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == user.Role);
        type.PlaceholderText = T("Choose User or Admin.");
        if (!await Dialog("Edit user type", Stack(Text(user.Username, "BodyStrongTextBlockStyle"), type), "Save")) return;
        if (type.SelectedItem == null) throw new ArgumentException("Choose User or Admin.");

        var selectedType = Choice(type);
        if (selectedType != user.Role)
            await Command(nameof(IMelonAdministration.SetUserType), new { userId = user.Id, type = selectedType });
    }

    private void DrawInvites()
    {
        if (inviteRows == null) return;
        inviteRows.Children.Clear();
        foreach (var invite in activeInvites.Where(i => i.ExpiresUtc > DateTime.UtcNow))
        {
            var show = ButtonFor(invite.Code, () => ShowCode(invite, null), refresh: false);
            inviteRows.Children.Add(Card(Row(Stack(show, Text(InviteTime(invite.ExpiresUtc), "CaptionTextBlockStyle")),
                ButtonFor("Invalidate", () => Command(nameof(IMelonAdministration.RevokeInvite), new { code = invite.Code })))));
        }
        if (inviteRows.Children.Count == 0) inviteRows.Children.Add(Text(T("No active invites.")));
    }

    private async Task RefreshInvites()
    {
        var rows = inviteRows;
        var invites = await Read<AccessCode[]>(nameof(IMelonAdministration.GetInvites));
        if (rows == null || rows != inviteRows || tab != "Users") return;
        if (!activeInvites.SequenceEqual(invites)) { activeInvites = invites; DrawInvites(); return; }
        // Preserve keyboard focus while refreshing the countdowns.
        for (int i = 0; i < invites.Length && i < rows.Children.Count; i++)
            if (rows.Children[i] is Border { Child: Grid row } && row.Children[0] is StackPanel stack && stack.Children[1] is TextBlock time)
                time.Text = InviteTime(invites[i].ExpiresUtc);
    }

    /// <summary>A QR code on a white square, 220 px.</summary>
    private static async Task<UIElement> QrImage(string text)
    {
        var source = new SvgImageSource { RasterizePixelWidth = 220, RasterizePixelHeight = 220 };
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(System.Text.Encoding.UTF8.GetBytes(QrCode.EncodeText(text, QrCode.Ecc.Medium).ToSvgString(4)));
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0); await source.SetSourceAsync(stream);
        return new Border { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center, Child = new Image { Source = source, Width = 220, Height = 220 } };
    }

    private async Task ShowCode(AccessCode grant, string? username)
    {
        var content = Stack();
        if (username == null) content.Children.Add(await QrImage(grant.Link ?? grant.Code));
        else content.Children.Add(Text(username, "BodyStrongTextBlockStyle"));
        var code = Text(grant.Code); code.IsTextSelectionEnabled = true;
        content.Children.Add(code);
        content.Children.Add(Text(T("Expires") + " " + grant.ExpiresUtc.ToLocalTime().ToString("g")));
        if (username == null && await InviteReach() is string reach) content.Children.Add(Text(T(reach), "CaptionTextBlockStyle"));
        var message = username == null && grant.Link != null ? grant.Code + "\n" + grant.Link : grant.Code;
        if (username != null)
        {
            var address = T("your Melon server") + " · /auth/reset-password";
            if (Uri.TryCreate(settings?.ListeningUrl, UriKind.Absolute, out var uri) && !uri.IsLoopback &&
                uri.HostNameType != UriHostNameType.Unknown && uri.Host is not ("0.0.0.0" or "[::]"))
                address = new Uri(uri, "/auth/reset-password").AbsoluteUri;
            content.Children.Add(Text(T("Enter the code at") + " " + address));
            message = T("Password reset") + " · " + username + "\n" + grant.Code + "\n" + address + "\n" + T("Expires") + " " + grant.ExpiresUtc.ToLocalTime().ToString("g");
        }
        var copy = new Button { Content = T("Copy") };
        copy.Click += (_, _) => { try { var data = new DataPackage(); data.SetText(message); Clipboard.SetContent(data); copy.Content = T("Copied"); } catch (Exception ex) { Error(ex); } };
        var email = new Button { Content = T("Compose email") };
        email.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("mailto:?subject=" + Uri.EscapeDataString("Melon · " + T(username == null ? "Invite" : "Password reset")) +
                "&body=" + Uri.EscapeDataString(message)) { UseShellExecute = true }); } catch (Exception ex) { Error(ex); }
        };
        content.Children.Add(Row(copy, email));
        await new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T(username == null ? "Invite" : "Password reset"),
            Content = content, CloseButtonText = T("Done") }.ShowAsync();
    }
}
