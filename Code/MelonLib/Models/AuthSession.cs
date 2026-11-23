using System;
using System.Collections.Generic;

namespace Melon.Models
{
    /// <summary>
    /// One signed-in device. Holds a hash of its current refresh token (never the token itself); each refresh replaces it,
    /// and presenting an older one revokes the session because the token must have been copied.
    /// </summary>
    public class AuthSession
    {
        public string _id { get; set; }
        public string UserId { get; set; }
        public string DeviceName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastUsedUtc { get; set; }
        /// <summary>Sliding expiry, pushed forward on every refresh.</summary>
        public DateTime ExpiresUtc { get; set; }
        public string RefreshHash { get; set; }
        /// <summary>The token replaced by the latest refresh, accepted briefly in case that response never reached the client.</summary>
        public string PreviousHash { get; set; }
        public DateTime RotatedUtc { get; set; }
        /// <summary>Older refresh tokens; presenting one means the session was copied.</summary>
        public List<string> UsedHashes { get; set; } = new();
        /// <summary>Stamp of the password when the session started: any password change ends the session.</summary>
        public string PasswordStamp { get; set; }
        public bool Revoked { get; set; }
        public string LastAddress { get; set; }
    }
}
