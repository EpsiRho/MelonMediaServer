using Melon.Interface;
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
