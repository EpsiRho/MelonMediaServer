using System.Security.Claims;
using System.Text;
using Melon.LocalClasses;
using Melon.Models;
using MelonWebApi.Middleware;
using MelonWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MelonWebApi.Controllers
{
    public sealed class TokenRequest { public string Username { get; set; } public string Password { get; set; } public string DeviceName { get; set; } }
    public sealed class RefreshRequest { public string RefreshToken { get; set; } }
    public sealed class RegisterRequest { public string InviteCode { get; set; } public string Username { get; set; } public string Password { get; set; } public string DeviceName { get; set; } }
    public sealed class RedeemLinkRequest { public string Code { get; set; } public string DeviceName { get; set; } }
    public sealed class PasswordChangeRequest { public string CurrentPassword { get; set; } public string NewPassword { get; set; } }
    public sealed class RevokeSessionRequest { public string SessionId { get; set; } }

    public sealed record TokenUser(string Id, string Username, string Role);
    public sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string SessionId, TokenUser User);
    public sealed record SessionInfo(string Id, string DeviceName, DateTime CreatedUtc, DateTime LastUsedUtc, string LastAddress, bool Current);

    /// <summary>
    /// Session-based sign-in. Credentials only ever travel in a POST body or the Authorization header, clients keep a
    /// refresh token instead of a password, and every signed-in device can be listed and signed out.
    /// </summary>
    [ApiController]
    public class SessionAuthController : ControllerBase
    {
        private static readonly byte[] DummySalt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
        private static readonly Lazy<string> DummyHash = new(() => Security.HashPasword(Guid.NewGuid().ToString(), DummySalt));

        private static IMongoCollection<User> Users => StateManager.DbClient.GetDatabase("Melon").GetCollection<User>("Users");
        private string Address => HttpContext.Connection.RemoteIpAddress?.ToString();
        private string CurrentUserId => User.FindFirstValue(ClaimTypes.UserData);
        private static TokenResponse Tokens(AuthSessions.Issued i) =>
            new(i.AccessToken, i.RefreshToken, i.ExpiresIn, i.Session._id, new TokenUser(i.User._id, i.User.Username, i.User.Type));

        /// <summary>
        /// Abilities newer than the original API, readable before signing in so clients can pick the right sign-in flow.
        /// "auth-v2": this controller (the only way to sign in).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("api/server/features")]
        public IActionResult Features()
        {
            return Ok(new List<string> { "stat-filters", "auth-v2" });
        }

        /// <summary>
        /// Signs in and starts a session. Send the credentials as <c>Authorization: Basic base64(username:password)</c>
        /// (or as JSON <c>{username, password}</c>); <c>deviceName</c> labels the session in the devices list.
        /// </summary>
        [AllowAnonymous]
        [EnableRateLimiting(Program.AuthRateLimit)]
        [HttpPost("auth/token")]
        public IActionResult Token([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] TokenRequest body)
        {
            string username = body?.Username, password = body?.Password;
            var header = Request.Headers.Authorization.ToString();
            if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
                    int colon = pair.IndexOf(':');
                    if (colon > 0) { username = pair[..colon]; password = pair[(colon + 1)..]; }
                }
                catch (FormatException) { return BadRequest("Malformed Authorization header."); }
            }

            var wait = LoginThrottle.RetryAfter(username, Address);
            if (wait > TimeSpan.Zero)
            {
                Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
                SecurityLog.Record(SecurityEventKinds.SignInThrottled, username: username, deviceName: body?.DeviceName);
                return StatusCode(429, "Too many failed attempts. Try again later.");
            }
            if (string.IsNullOrEmpty(username) || UserAccounts.CheckPassword(password) != null)
            {
                SecurityLog.Record(SecurityEventKinds.SignInFailed, username: username, deviceName: body?.DeviceName, detail: "Missing or malformed credentials");
                return Unauthorized("Invalid username or password");
            }

            var user = Users.Find(u => u.Username == username).FirstOrDefault();
            // Hash even for unknown usernames, so timing doesn't reveal which accounts exist.
            bool ok = user != null ? Security.VerifyPassword(password, user.Password, user.Salt)
                                   : Security.VerifyPassword(password, DummyHash.Value, DummySalt) && false;
            if (!ok)
            {
                LoginThrottle.Failed(username, Address);
                SecurityLog.Record(SecurityEventKinds.SignInFailed, user?._id, username, deviceName: body?.DeviceName,
                    detail: user == null ? "Unknown username" : "Wrong password");
                return Unauthorized("Invalid username or password");
            }
            LoginThrottle.Succeeded(username, Address);
            var issued = AuthSessions.Start(user, body?.DeviceName, Address);
            SecurityLog.Record(SecurityEventKinds.SignIn, user._id, user.Username, issued.Session._id, issued.Session.DeviceName);
            return Ok(Tokens(issued));
        }

        /// <summary>Swaps a refresh token for a new access token and a new refresh token (the old one stops working).</summary>
        [AllowAnonymous]
        [EnableRateLimiting(Program.AuthRateLimit)]
        [HttpPost("auth/refresh")]
        public IActionResult Refresh([FromBody] RefreshRequest body)
        {
            var (issued, failure) = AuthSessions.Refresh(body?.RefreshToken, Address);
            if (issued == null)
            {
                return Unauthorized(failure switch
                {
                    AuthSessions.RefreshFailure.PasswordChanged => "The password was changed. Sign in again.",
                    AuthSessions.RefreshFailure.Reused => "This sign-in was used elsewhere and has been ended. Sign in again.",
                    AuthSessions.RefreshFailure.Expired => "This sign-in has expired. Sign in again.",
                    _ => "Sign in again.",
                });
            }
            return Ok(Tokens(issued));
        }

        /// <summary>Ends a session: the one the refresh token belongs to, or else the caller's own.</summary>
        [AllowAnonymous]
        [HttpPost("auth/logout")]
        public IActionResult Logout([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshRequest body)
        {
            var sid = User.Identity?.IsAuthenticated == true ? TokenRules.SessionId(User) : null;
            if (!string.IsNullOrEmpty(body?.RefreshToken))
            {
                var hash = AuthSessions.Hash(body.RefreshToken);
                var s = StateManager.DbClient.GetDatabase("Melon").GetCollection<AuthSession>("Sessions")
                    .Find(x => x.RefreshHash == hash || x.PreviousHash == hash).FirstOrDefault();
                sid = s?._id ?? sid;
            }
            if (sid != null && AuthSessions.Get(sid) is { Revoked: false } ended)
            {
                AuthSessions.Revoke(sid);
                SecurityLog.Record(SecurityEventKinds.SignOut, ended.UserId, sessionId: sid, deviceName: ended.DeviceName);
            }
            return NoContent();
        }

        /// <summary>A read-only token for media URLs (<c>?jwt=</c> on api/download/*), valid 6 hours, tied to this session.</summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("auth/media-token")]
        public IActionResult MediaToken()
        {
            var sid = TokenRules.SessionId(User);
            if (sid == null) return BadRequest("Sign in with auth/token first.");
            var user = Users.Find(u => u._id == CurrentUserId).FirstOrDefault();
            if (user == null) return Unauthorized();
            return Ok(new { token = AuthSessions.AccessToken(user, sid, AuthSessions.MediaScope), expiresIn = (int)AuthSessions.MediaLifetime.TotalSeconds });
        }

        /// <summary>Creates an account with an invite code and signs it in, in one call.</summary>
        [AllowAnonymous]
        [EnableRateLimiting(Program.AuthRateLimit)]
        [HttpPost("auth/register")]
        public IActionResult Register([FromBody] RegisterRequest body)
        {
            var invalid = UserAccounts.CheckUsername(body?.Username) ?? UserAccounts.CheckPassword(body?.Password);
            if (invalid != null) return BadRequest(invalid);
            if (string.IsNullOrEmpty(body.InviteCode) || body.InviteCode.Length > 64) return Unauthorized("Invalid or expired invite code.");
            var users = Users;
            if (UserAccounts.UsernameTaken(users, body.Username)) return Conflict("Username is already taken.");
            if (!Security.RedeemInvite(body.InviteCode.Trim().ToUpperInvariant())) return Unauthorized("Invalid or expired invite code.");

            var user = new User
            {
                _id = ObjectId.GenerateNewId().ToString(), Username = body.Username, Type = "User",
                Password = Security.HashPassword(body.Password, out var salt), Salt = salt,
                Bio = "", FavTrack = "", FavAlbum = "", FavArtist = "", Friends = [], LastLogin = DateTime.UtcNow,
            };
            try { users.InsertOne(user); }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return Conflict("Username is already taken."); }
            ServerAdministration.Instance.PublishMessage("users", "Account created.");
            var started = AuthSessions.Start(user, body.DeviceName, Address);
            SecurityLog.Record(SecurityEventKinds.AccountCreated, user._id, user.Username, started.Session._id, started.Session.DeviceName, "Invite");
            return Ok(Tokens(started));
        }

        /// <summary>
        /// Starts signing in another device: a single-use code, valid 2 minutes, shown as a QR of
        /// <c>https://server/auth/link?code=…</c>. No password or long-lived token is in the code.
        /// </summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("auth/device-link")]
        public IActionResult CreateDeviceLink()
        {
            try
            {
                var link = AuthSessions.CreateDeviceLink(CurrentUserId);
                SecurityLog.Record(SecurityEventKinds.DeviceLinkCreated, CurrentUserId, User.Identity?.Name, TokenRules.SessionId(User));
                return Ok(new { id = link.Id, code = link.Code, expiresAt = link.ExpiresUtc });
            }
            catch (InvalidOperationException ex) { return StatusCode(429, ex.Message); }
        }

        /// <summary>The new device trades the scanned code for its own session.</summary>
        [AllowAnonymous]
        [EnableRateLimiting(Program.AuthRateLimit)]
        [HttpPost("auth/device-link/redeem")]
        public IActionResult RedeemDeviceLink([FromBody] RedeemLinkRequest body)
        {
            var issued = AuthSessions.RedeemDeviceLink(body?.Code?.Trim(), body?.DeviceName, Address);
            if (issued == null) return Unauthorized("This sign-in code is invalid, used or expired.");
            SecurityLog.Record(SecurityEventKinds.DeviceLinkRedeemed, issued.User._id, issued.User.Username, issued.Session._id, issued.Session.DeviceName);
            return Ok(Tokens(issued));
        }

        /// <summary>For the device showing the code: whether a device has used it yet.</summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("auth/device-link/{id}")]
        public IActionResult DeviceLinkStatus(string id)
        {
            var s = AuthSessions.DeviceLinkStatus(id, CurrentUserId);
            if (!s.Found) return NotFound();
            return Ok(new { redeemed = s.Redeemed, deviceName = s.Device, expiresAt = s.ExpiresUtc });
        }

        /// <summary>Pages for links opened in a browser instead of the app (no data from the request is echoed).</summary>
        [AllowAnonymous]
        [HttpGet("auth/join")]
        public ContentResult JoinPage() => Page("You're invited", "Open Galia, choose <b>Sign up</b>, and scan this QR code or paste this page's address to create your account.");

        [AllowAnonymous]
        [HttpGet("auth/link")]
        public ContentResult LinkPage() => Page("Sign in on this device", "Open Galia on the device you want to sign in, choose <b>Sign in</b> → <b>Scan QR code</b>, and scan the code again. It expires in 2 minutes.");

        private ContentResult Page(string title, string body)
        {
            Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Content($"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Melon</title>" +
                "<style>body{font-family:system-ui,sans-serif;max-width:32rem;margin:15vh auto;padding:0 1rem;line-height:1.5;color:#222}h1{font-size:1.5rem}</style></head>" +
                $"<body><h1>{title}</h1><p>{body}</p></body></html>", "text/html; charset=utf-8");
        }

        /// <summary>Changes the caller's password. Needs the current one; signs out every other device.</summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [EnableRateLimiting(Program.AuthRateLimit)]
        [HttpPatch("api/users/password")]
        public IActionResult ChangePassword([FromBody] PasswordChangeRequest body)
        {
            var user = Users.Find(u => u._id == CurrentUserId).FirstOrDefault();
            if (user == null) return Unauthorized();
            var invalid = UserAccounts.CheckPassword(body?.NewPassword);
            if (invalid != null) return BadRequest(invalid);
            var wait = LoginThrottle.RetryAfter(user.Username, Address);
            if (wait > TimeSpan.Zero)
            {
                SecurityLog.Record(SecurityEventKinds.PasswordChangeFailed, user._id, user.Username, TokenRules.SessionId(User), detail: "Throttled");
                return StatusCode(429, "Too many failed attempts. Try again later.");
            }
            if (UserAccounts.CheckPassword(body.CurrentPassword) != null || !Security.VerifyPassword(body.CurrentPassword, user.Password, user.Salt))
            {
                LoginThrottle.Failed(user.Username, Address);
                SecurityLog.Record(SecurityEventKinds.PasswordChangeFailed, user._id, user.Username, TokenRules.SessionId(User), detail: "Wrong current password");
                return StatusCode(403, "The current password is wrong.");
            }
            LoginThrottle.Succeeded(user.Username, Address);

            user.Password = Security.HashPassword(body.NewPassword, out var salt);
            user.Salt = salt;
            Users.UpdateOne(u => u._id == user._id, Builders<User>.Update.Set(u => u.Password, user.Password).Set(u => u.Salt, user.Salt));
            var sid = TokenRules.SessionId(User);
            AuthSessions.RevokeAll(user._id, sid);
            SecurityLog.Record(SecurityEventKinds.PasswordChanged, user._id, user.Username, sid, detail: "Other devices signed out");
            // The old stamp is gone, so this session needs a new access token to keep working.
            return Ok(new { accessToken = sid != null ? AuthSessions.Restamp(sid, user) : null, expiresIn = (int)AuthSessions.AccessLifetime.TotalSeconds });
        }

        /// <summary>The caller's signed-in devices. Admins may pass <c>userId</c> to see someone else's.</summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("api/users/sessions")]
        public IActionResult Sessions(string userId = null)
        {
            var target = string.IsNullOrEmpty(userId) ? CurrentUserId : userId;
            if (target != CurrentUserId && !User.IsInRole("Admin")) return Forbid();
            var sid = TokenRules.SessionId(User);
            return Ok(AuthSessions.List(target).Select(s => new SessionInfo(s._id, s.DeviceName, s.CreatedUtc, s.LastUsedUtc, s.LastAddress, s._id == sid)));
        }

        /// <summary>Signs out one device: the caller's own, or anyone's for admins.</summary>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("api/users/sessions/revoke")]
        public IActionResult RevokeSession([FromBody] RevokeSessionRequest body)
        {
            var s = string.IsNullOrEmpty(body?.SessionId) ? null : AuthSessions.Get(body.SessionId);
            if (s == null || s.Revoked) return NotFound();
            if (s.UserId != CurrentUserId && !User.IsInRole("Admin")) return NotFound();
            AuthSessions.Revoke(s._id);
            SecurityLog.Record(SecurityEventKinds.SessionRevoked, s.UserId, sessionId: s._id, deviceName: s.DeviceName);
            return NoContent();
        }
    }
}
