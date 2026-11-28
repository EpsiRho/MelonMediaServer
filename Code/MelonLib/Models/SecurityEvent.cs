using System;

namespace Melon.Models
{
    /// <summary>
    /// Something security-relevant happened: a sign-in, a failed attempt, a device signed out, an account change.
    /// Melon keeps these for 90 days (shown in MelonCLI / MelonGUI → Security), writes them to the server log, and raises
    /// them to plugins through <c>IWebApi.SecurityEventRaised</c>.
    /// </summary>
    public class SecurityEvent
    {
        public string _id { get; set; }
        public DateTime TimeUtc { get; set; }
        /// <summary>One of the names in <see cref="SecurityEventKinds"/>.</summary>
        public string Kind { get; set; }
        /// <summary>Worth a look: failures, throttling, a refresh token used twice.</summary>
        public bool Warning { get; set; }
        /// <summary>The account the event is about, when known.</summary>
        public string UserId { get; set; }
        /// <summary>The account's name, or the name that was tried for a failed sign-in.</summary>
        public string Username { get; set; }
        /// <summary>Who did it, when that's someone else (an admin deleting an account). Empty for local controllers.</summary>
        public string ActorId { get; set; }
        /// <summary>Client address for web requests, or "controller" for MelonCLI / MelonGUI.</summary>
        public string Address { get; set; }
        public string SessionId { get; set; }
        public string DeviceName { get; set; }
        /// <summary>Short extra context, such as the new role. Never a password, token or code.</summary>
        public string Detail { get; set; }
    }

    /// <summary>The kinds of <see cref="SecurityEvent"/>.</summary>
    public static class SecurityEventKinds
    {
        public const string SignIn = "SignIn";
        public const string SignInFailed = "SignInFailed";
        public const string SignInThrottled = "SignInThrottled";
        public const string SignOut = "SignOut";
        public const string SessionRevoked = "SessionRevoked";
        /// <summary>An old refresh token came back: likely copied. The session was ended.</summary>
        public const string TokenReuse = "TokenReuse";
        public const string SignOutEveryone = "SignOutEveryone";
        public const string DeviceLinkCreated = "DeviceLinkCreated";
        public const string DeviceLinkRedeemed = "DeviceLinkRedeemed";
        public const string AccountCreated = "AccountCreated";
        public const string AccountDeleted = "AccountDeleted";
        public const string RoleChanged = "RoleChanged";
        public const string UsernameChanged = "UsernameChanged";
        public const string PasswordChanged = "PasswordChanged";
        public const string PasswordChangeFailed = "PasswordChangeFailed";
        public const string PasswordResetIssued = "PasswordResetIssued";
        public const string PasswordReset = "PasswordReset";
        public const string InviteCreated = "InviteCreated";
        public const string InviteRevoked = "InviteRevoked";
        public const string RemoteAccessEnabled = "RemoteAccessEnabled";
        public const string RemoteAccessDisabled = "RemoteAccessDisabled";
        /// <summary>The first request of the day from an address outside the home network.</summary>
        public const string RemoteClientSeen = "RemoteClientSeen";
        /// <summary>A request from outside the home network while Remote Access was off (or over plain HTTP).</summary>
        public const string OutsideRequestRefused = "OutsideRequestRefused";
        public const string AddressBlocked = "AddressBlocked";
        public const string AddressUnblocked = "AddressUnblocked";
        /// <summary>A blocked address tried again (recorded at most once an hour per address).</summary>
        public const string BlockedRequest = "BlockedRequest";
    }
}
