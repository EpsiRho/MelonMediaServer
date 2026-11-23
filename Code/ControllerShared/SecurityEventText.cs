using Melon.Models;

namespace Melon.Controllers;

/// <summary>Plain-language names for <see cref="SecurityEvent"/> kinds, shared by MelonCLI and MelonGUI (translate with UiStrings).</summary>
internal static class SecurityEventText
{
    public static string Label(string kind) => kind switch
    {
        SecurityEventKinds.SignIn => "Signed in",
        SecurityEventKinds.SignInFailed => "Failed sign-in",
        SecurityEventKinds.SignInThrottled => "Sign-in blocked (too many attempts)",
        SecurityEventKinds.SignOut => "Signed out",
        SecurityEventKinds.SessionRevoked => "Device signed out",
        SecurityEventKinds.TokenReuse => "Possible stolen sign-in",
        SecurityEventKinds.SignOutEveryone => "Every device signed out",
        SecurityEventKinds.DeviceLinkCreated => "Sign-in code shown",
        SecurityEventKinds.DeviceLinkRedeemed => "Signed in with a code",
        SecurityEventKinds.AccountCreated => "Account created",
        SecurityEventKinds.AccountDeleted => "Account deleted",
        SecurityEventKinds.RoleChanged => "Account type changed",
        SecurityEventKinds.UsernameChanged => "Username changed",
        SecurityEventKinds.PasswordChanged => "Password changed",
        SecurityEventKinds.PasswordChangeFailed => "Failed password change",
        SecurityEventKinds.PasswordResetIssued => "Reset code created",
        SecurityEventKinds.PasswordReset => "Password reset",
        SecurityEventKinds.InviteCreated => "Invite created",
        SecurityEventKinds.InviteRevoked => "Invite revoked",
        _ => kind,
    };

    /// <summary>"device · address · detail", skipping empty parts.</summary>
    public static string Where(SecurityEvent e) =>
        string.Join("  ·  ", new[] { e.DeviceName, e.Address, e.Detail }.Where(s => !string.IsNullOrEmpty(s)));
}
