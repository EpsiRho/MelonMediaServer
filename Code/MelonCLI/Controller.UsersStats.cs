using Melon.Controllers;
using Melon.Interface;
using Melon.Models;
using Net.Codecrete.QrCodeGenerator;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private async Task Users(Screen page)
    {
        var users = status!.DatabaseConnected ? await Read<ManagedUser[]>(nameof(Admin.GetUsers)) : [];
        var invites = (await Read<AccessCode[]>(nameof(Admin.GetInvites))).OrderBy(i => i.ExpiresUtc).ToArray();
        int admins = users.Count(u => u.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        page.Body.Add(new Cards([
            new Card(T("Users"), $"{users.Length:N0}", status.DatabaseConnected ? null : T("Database offline"), Tone.Warn),
            new Card(T("Admin"), $"{admins:N0}"),
            new Card(T("Invites"), $"{invites.Length:N0}",
                invites.Length > 0 ? T("Next expires") + " " + invites[0].ExpiresUtc.ToLocalTime().ToString("t") : T("No active invites."))], 18));

        page.Items.Add(Entry.Header(T("Invites")));
        page.Items.Add(Action("Create invite", async () =>
        {
            if (!int.TryParse(await Ask("Expires in (minutes)", "10", help: T("Anyone with the code can create an account until it expires.")), out int minutes) || minutes < 1)
                throw new ArgumentException(T("Enter a positive number of minutes."));
            await Code(await Read<AccessCode>(nameof(Admin.CreateInvite), new { minutes }), null);
        }));
        foreach (var grant in invites)
            page.Items.Add(new Entry(grant.Code, () => Code(grant, null)) { Value = Remaining(grant), ValueTone = Urgency(grant) });
        if (users.Length > 0) page.Items.Add(Entry.Header(T("Users")));
        foreach (var user in users.OrderBy(u => u.Username))
        {
            page.Items.Add(new Entry(user.Username, async () =>
            {
                if (await Choose(user.Username, ["Reset code", "Delete user"]) == "Reset code")
                    await Code(await Read<AccessCode>(nameof(Admin.CreatePasswordReset), new { userId = user.Id }), user.Username);
                else
                {
                    await Confirm("Delete user?", user.Username + "\n" + T("Listening stats are kept. You can prune them separately in Stats."), true);
                    await Write(nameof(Admin.DeleteUser), new { userId = user.Id });
                    Say(T("User deleted."));
                }
            }) { Value = T(user.Role) });
        }

        // Security settings live here, not in the web API, so a stolen admin token can't change them.
        var config = await Read<ControllerSettings>(nameof(Admin.GetSettings));
        page.Items.Add(Entry.Header(T("Security")));
        page.Items.Add(Setting("Artwork upload limit (MB)", config.ArtUploadMegabytes.ToString(), async () =>
        {
            if (!int.TryParse(await Ask("Artwork upload limit (MB)", config.ArtUploadMegabytes.ToString(), help: T("1–200 MB.")), out int art))
                throw new ArgumentException(T("Enter a whole number."));
            await Write(nameof(Admin.SetUploadLimits), new { artMegabytes = art, playlistMegabytes = config.PlaylistImportMegabytes });
        }));
        page.Items.Add(Setting("Playlist import limit (MB)", config.PlaylistImportMegabytes.ToString(), async () =>
        {
            if (!int.TryParse(await Ask("Playlist import limit (MB)", config.PlaylistImportMegabytes.ToString(), help: T("1–100 MB.")), out int lists))
                throw new ArgumentException(T("Enter a whole number."));
            await Write(nameof(Admin.SetUploadLimits), new { artMegabytes = config.ArtUploadMegabytes, playlistMegabytes = lists });
        }));
        page.Items.Add(Setting("Log file size (MB)", config.LogFileMegabytes.ToString(), async () =>
        {
            if (!int.TryParse(await Ask("Log file size (MB)", config.LogFileMegabytes.ToString(), help: T("1–1024 MB. A new file starts at this size. Applies after a restart.")), out int size))
                throw new ArgumentException(T("Enter a whole number."));
            await Write(nameof(Admin.SetLogLimits), new { fileMegabytes = size, filesKept = config.LogFilesKept });
        }));
        page.Items.Add(Setting("Log files kept", config.LogFilesKept.ToString(), async () =>
        {
            if (!int.TryParse(await Ask("Log files kept", config.LogFilesKept.ToString(), help: T("1–100. Older files are deleted. Applies after a restart.")), out int kept))
                throw new ArgumentException(T("Enter a whole number."));
            await Write(nameof(Admin.SetLogLimits), new { fileMegabytes = config.LogFileMegabytes, filesKept = kept });
        }));
        if (status.DatabaseConnected)
        {
            var recent = await Read<SecurityEvent[]>(nameof(Admin.GetSecurityEvents), new { count = 200, warningsOnly = false });
            int warnings = recent.Count(e => e.Warning && e.TimeUtc > DateTime.UtcNow.AddDays(-1));
            page.Items.Add(new Entry(T("Security events"), () => SecurityEvents(recent))
            {
                Value = warnings > 0 ? $"{warnings:N0} " + T("warnings in 24 h") : T("No warnings in 24 h"),
                ValueTone = warnings > 0 ? Tone.Warn : Tone.Muted,
            });
        }
        page.Items.Add(Action("Sign out every device", async () =>
        {
            await Confirm("Sign out every device?", T("Every app on every device has to sign in again."), true);
            await Write(nameof(Admin.SignOutEveryone));
            Say(T("Every device was signed out."));
        }, Tone.Danger));
    }

    /// <summary>Sign-ins, failures and account changes, newest first (Melon keeps 90 days).</summary>
    private async Task SecurityEvents(SecurityEvent[] events)
    {
        if (events.Length == 0) { await Show("Security events", T("Nothing recorded yet.")); return; }
        var rows = events.Select(e => (new[]
        {
            e.TimeUtc.ToLocalTime().ToString("g"), T(SecurityEventText.Label(e.Kind)), e.Username ?? "", SecurityEventText.Where(e)
        }, 0d, e.Warning ? Tone.Warn : Tone.Normal));
        await ShowBlocks("Security events",
            new Para(T("Sign-ins, failed attempts, signed-out devices and account changes. Kept for 90 days."), Tone.Muted),
            new Table([T("Time"), T("Event"), T("User"), T("Details")], [false, false, false, false], rows));
    }

    private Tone Urgency(AccessCode grant)
    {
        var left = grant.ExpiresUtc.ToUniversalTime() - DateTime.UtcNow;
        return left <= TimeSpan.Zero ? Tone.Danger : left.TotalMinutes < 2 ? Tone.Warn : Tone.Muted;
    }

    private string Remaining(AccessCode grant)
    {
        var left = grant.ExpiresUtc.ToUniversalTime() - DateTime.UtcNow;
        if (left <= TimeSpan.Zero) return T("Expired");
        return $"{grant.ExpiresUtc.ToLocalTime():t}  ({(left.TotalHours >= 1 ? $"{Math.Ceiling(left.TotalHours):N0} h" : left.TotalMinutes >= 1 ? $"{Math.Ceiling(left.TotalMinutes):N0} min" : $"{Math.Ceiling(left.TotalSeconds):N0} s")})";
    }

    private async Task Code(AccessCode grant, string? username)
    {
        var title = username == null ? T("Invite") : T("Password reset") + " · " + username;
        string address = T("your Melon server") + " · /auth/reset-password";
        if (Uri.TryCreate(settings.ListeningUrl, UriKind.Absolute, out var uri) && !uri.IsLoopback &&
            uri.HostNameType != UriHostNameType.Unknown && uri.Host is not ("0.0.0.0" or "[::]"))
            address = new Uri(uri, "/auth/reset-password").AbsoluteUri;
        var message = username == null ? grant.Code : $"{username}\n{grant.Code}\n{T("Enter the code at")} {address}";
        var qr = username == null ? QrCode.EncodeText(grant.Code, QrCode.Ecc.Medium) : null;
        Screen View()
        {
            var page = new Screen(title);
            if (grant.ExpiresUtc.ToUniversalTime() <= DateTime.UtcNow)
            {
                page.Body.Add(new Callout(T("Create a new one to try again."), Tone.Danger, T("Expired")));
                return page;
            }
            page.Body.Add(new BigCode(grant.Code, T("Expires") + " " + Remaining(grant), Urgency(grant)));
            if (qr != null) { page.Body.Add(new QrBlock(qr)); page.Body.Add(new Para(T("Scan the QR code or share the code to create an account."), Tone.Muted)); }
            else page.Body.Add(new Panel(T("Password reset"), new KeyValues([(T("User"), username!, Tone.Normal), (T("Enter the code at"), address, Tone.Normal)])));
            var share = username == null ? grant.Code : message + "\n" + T("Expires") + " " + grant.ExpiresUtc.ToLocalTime().ToString("g");
            page.Items.Add(Action("Copy", async () => { await Platform.Copy(share, token); Say(T("Copied")); }));
            page.Items.Add(Action("Compose email", () => Platform.Launch("mailto:?subject=" + Uri.EscapeDataString("Melon · " + title) + "&body=" + Uri.EscapeDataString(share))));
            if (username == null) page.Items.Add(Action("Invalidate", async () => { await Write(nameof(Admin.RevokeInvite), new { code = grant.Code }); Say(T("Invite revoked.")); }, Tone.Danger));
            return page;
        }
        var selected = await ui.Menu(View(), () => Task.FromResult(View()));
        if (selected != null && grant.ExpiresUtc.ToUniversalTime() > DateTime.UtcNow) await selected.Run!();
    }
}
