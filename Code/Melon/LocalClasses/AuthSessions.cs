using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Melon.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Serilog;

namespace Melon.LocalClasses;

/// <summary>
/// Session-based sign-in ("auth v2"). A device signs in once and gets a short-lived access token plus a long-lived
/// refresh token. The refresh token is single use: each refresh returns a new one, and presenting an old one revokes
/// the whole session (it was copied). The signing key persists across restarts, so restarts no longer sign everyone out,
/// and clients never need to keep a password.
/// </summary>
public static class AuthSessions
{
    public const string SessionClaim = "melon_sid", ScopeClaim = "melon_scope", ApiScope = "api", MediaScope = "media";
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    /// <summary>
    /// Media tokens go in URLs handed to players and image loaders, which can't refresh them mid-stream, so they last
    /// longer; they only work on media download routes.
    /// </summary>
    public static readonly TimeSpan MediaLifetime = TimeSpan.FromHours(6);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(60);
    private static readonly TimeSpan PreviousTokenGrace = TimeSpan.FromMinutes(2);
    private const int MaxUsedHashes = 50;

    private static IMongoCollection<AuthSession> Sessions => StateManager.DbClient.GetDatabase("Melon").GetCollection<AuthSession>("Sessions");
    private static IMongoCollection<User> Users => StateManager.DbClient.GetDatabase("Melon").GetCollection<User>("Users");

    // ---------------------------------------------------------------- signing key

    private sealed class KeyFile { public string Key { get; set; } }

    /// <summary>The persisted signing key (protected with ASP.NET Data Protection), created on first use.</summary>
    public static byte[] LoadOrCreateKey()
    {
        try
        {
            var stored = Storage.LoadConfigFile<KeyFile>("AuthKey", ["Key"], out _);
            if (!string.IsNullOrEmpty(stored?.Key))
            {
                var key = Convert.FromBase64String(stored.Key);
                if (key.Length >= 32) return key;
            }
        }
        catch (Exception ex) { Log.Warning("Signing key unreadable ({ErrorType}); creating a new one, so devices must sign in again.", ex.GetType().Name); }
        return SaveNewKey();
    }

    private static byte[] SaveNewKey()
    {
        var key = RandomNumberGenerator.GetBytes(64);
        try { Storage.SaveConfigFile("AuthKey", new KeyFile { Key = Convert.ToBase64String(key) }, ["Key"]); }
        catch (Exception ex) { Log.Warning("Signing key couldn't be saved ({ErrorType}); restarts will sign everyone out.", ex.GetType().Name); }
        return key;
    }

    /// <summary>Rotates the signing key and revokes every session: every token everywhere stops working.</summary>
    public static void SignOutEveryone(IMongoDatabase? db)
    {
        StateManager.JWTKey = SaveNewKey();
        try { db?.GetCollection<AuthSession>("Sessions").UpdateMany(s => !s.Revoked, Builders<AuthSession>.Update.Set(s => s.Revoked, true)); }
        catch (Exception ex) { Log.Warning("Sessions couldn't be revoked: {Message}", ex.Message); }
        ActiveCache.Clear();
        SecurityLog.Record(SecurityEventKinds.SignOutEveryone, detail: "Signing key rotated; every session ended");
    }

    // ---------------------------------------------------------------- tokens

    public sealed record Issued(string AccessToken, string RefreshToken, int ExpiresIn, AuthSession Session, User User);

    private static string NewSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));
    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static string AccessToken(User user, string sessionId, string scope = ApiScope)
    {
        var lifetime = scope == MediaScope ? MediaLifetime : AccessLifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Type),
                new Claim(ClaimTypes.UserData, user._id),
                new Claim("melon_session", Security.SessionStamp(user.Password)),
                new Claim(SessionClaim, sessionId),
                new Claim(ScopeClaim, scope),
            ]),
            Issuer = "melon",
            Expires = DateTime.UtcNow.Add(lifetime),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(StateManager.JWTKey), SecurityAlgorithms.HmacSha256Signature)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>Starts a new session for <paramref name="user"/> on <paramref name="deviceName"/>.</summary>
    public static Issued Start(User user, string? deviceName, string? address)
    {
        var refresh = NewSecret();
        var now = DateTime.UtcNow;
        var session = new AuthSession
        {
            _id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            UserId = user._id,
            DeviceName = CleanDeviceName(deviceName),
            CreatedUtc = now, LastUsedUtc = now, RotatedUtc = now,
            ExpiresUtc = now + SessionLifetime,
            RefreshHash = Hash(refresh),
            PasswordStamp = Security.SessionStamp(user.Password),
            LastAddress = address,
        };
        Sessions.InsertOne(session);
        Users.UpdateOne(u => u._id == user._id, Builders<User>.Update.Set(u => u.LastLogin, now));
        return new(AccessToken(user, session._id), refresh, (int)AccessLifetime.TotalSeconds, session, user);
    }

    public enum RefreshFailure { None, Invalid, Expired, Reused, PasswordChanged }

    /// <summary>
    /// Swaps a refresh token for a new access + refresh token. A token used before revokes the session, except the one
    /// replaced moments ago (the client may not have received the reply), which gets a fresh pair once more.
    /// </summary>
    public static (Issued? Result, RefreshFailure Failure) Refresh(string? refreshToken, string? address)
    {
        if (string.IsNullOrEmpty(refreshToken) || refreshToken.Length > 200) return (null, RefreshFailure.Invalid);
        var hash = Hash(refreshToken);
        var session = Sessions.Find(Builders<AuthSession>.Filter.Or(
            Builders<AuthSession>.Filter.Eq(s => s.RefreshHash, hash),
            Builders<AuthSession>.Filter.Eq(s => s.PreviousHash, hash),
            Builders<AuthSession>.Filter.AnyEq(s => s.UsedHashes, hash))).FirstOrDefault();
        if (session == null || session.Revoked) return (null, RefreshFailure.Invalid);
        var now = DateTime.UtcNow;
        if (session.ExpiresUtc <= now) { Revoke(session._id); return (null, RefreshFailure.Expired); }

        bool current = session.RefreshHash == hash;
        bool graceful = !current && session.PreviousHash == hash && now - session.RotatedUtc < PreviousTokenGrace;
        if (!current && !graceful)
        {
            Revoke(session._id);
            SecurityLog.Record(SecurityEventKinds.TokenReuse, session.UserId, sessionId: session._id, deviceName: session.DeviceName,
                detail: "An old refresh token was used again; the session was ended", address: address);
            return (null, RefreshFailure.Reused);
        }

        var user = Users.Find(u => u._id == session.UserId).FirstOrDefault();
        if (user == null) { Revoke(session._id); return (null, RefreshFailure.Invalid); }
        if (session.PasswordStamp != Security.SessionStamp(user.Password)) { Revoke(session._id); return (null, RefreshFailure.PasswordChanged); }

        var refresh = NewSecret();
        var newHash = Hash(refresh);
        var used = current ? session.PreviousHash : null;
        var update = Builders<AuthSession>.Update
            .Set(s => s.RefreshHash, newHash)
            .Set(s => s.PreviousHash, current ? hash : session.PreviousHash)
            .Set(s => s.RotatedUtc, now)
            .Set(s => s.LastUsedUtc, now)
            .Set(s => s.ExpiresUtc, now + SessionLifetime)
            .Set(s => s.LastAddress, address);
        if (!current) update = update.Push(s => s.UsedHashes, session.RefreshHash); // the grace swap retires the unseen token
        else if (used != null) update = update.PushEach(s => s.UsedHashes, [used], slice: -MaxUsedHashes);
        // Only the caller holding the token we matched wins a concurrent refresh.
        var matched = Builders<AuthSession>.Filter.Eq(s => s._id, session._id) &
                      (current ? Builders<AuthSession>.Filter.Eq(s => s.RefreshHash, hash) : Builders<AuthSession>.Filter.Eq(s => s.PreviousHash, hash));
        if (Sessions.UpdateOne(matched, update).ModifiedCount == 0) return (null, RefreshFailure.Invalid);
        session.RefreshHash = newHash;
        return (new(AccessToken(user, session._id), refresh, (int)AccessLifetime.TotalSeconds, session, user), RefreshFailure.None);
    }

    /// <summary>Gives the session a new password stamp (after its own user changed the password) and a fresh access token.</summary>
    public static string Restamp(string sessionId, User user)
    {
        Sessions.UpdateOne(s => s._id == sessionId, Builders<AuthSession>.Update.Set(s => s.PasswordStamp, Security.SessionStamp(user.Password)));
        ActiveCache.TryRemove(sessionId, out _);
        return AccessToken(user, sessionId);
    }

    // ---------------------------------------------------------------- session management

    public static List<AuthSession> List(string userId) =>
        Sessions.Find(s => s.UserId == userId && !s.Revoked && s.ExpiresUtc > DateTime.UtcNow).SortByDescending(s => s.LastUsedUtc).ToList();

    public static AuthSession? Get(string sessionId) => Sessions.Find(s => s._id == sessionId).FirstOrDefault();

    public static void Revoke(string sessionId)
    {
        Sessions.UpdateOne(s => s._id == sessionId, Builders<AuthSession>.Update.Set(s => s.Revoked, true));
        ActiveCache.TryRemove(sessionId, out _);
    }

    public static void RevokeAll(string userId, string? exceptSessionId = null)
    {
        var filter = Builders<AuthSession>.Filter.Eq(s => s.UserId, userId) & Builders<AuthSession>.Filter.Eq(s => s.Revoked, false);
        if (exceptSessionId != null) filter &= Builders<AuthSession>.Filter.Ne(s => s._id, exceptSessionId);
        Sessions.UpdateMany(filter, Builders<AuthSession>.Update.Set(s => s.Revoked, true));
        foreach (var key in ActiveCache.Keys) if (key != exceptSessionId) ActiveCache.TryRemove(key, out _);
    }

    private static readonly ConcurrentDictionary<string, (bool Active, DateTime Checked)> ActiveCache = new();

    /// <summary>Whether the session still exists for this user and isn't revoked or expired (cached for 30 seconds).</summary>
    public static bool IsActive(string sessionId, string userId)
    {
        if (ActiveCache.TryGetValue(sessionId, out var c) && DateTime.UtcNow - c.Checked < TimeSpan.FromSeconds(30)) return c.Active;
        var s = Get(sessionId);
        bool active = s != null && !s.Revoked && s.UserId == userId && s.ExpiresUtc > DateTime.UtcNow;
        ActiveCache[sessionId] = (active, DateTime.UtcNow);
        if (ActiveCache.Count > 10_000) ActiveCache.Clear();
        return active;
    }

    /// <summary>Indexes for refresh lookups and per-user listing; expired sessions are deleted by MongoDB a week after expiry.</summary>
    public static void EnsureIndexes(IMongoDatabase db)
    {
        try
        {
            var c = db.GetCollection<AuthSession>("Sessions");
            c.Indexes.CreateMany([
                new CreateIndexModel<AuthSession>(Builders<AuthSession>.IndexKeys.Ascending(s => s.RefreshHash)),
                new CreateIndexModel<AuthSession>(Builders<AuthSession>.IndexKeys.Ascending(s => s.PreviousHash)),
                new CreateIndexModel<AuthSession>(Builders<AuthSession>.IndexKeys.Ascending(s => s.UsedHashes)),
                new CreateIndexModel<AuthSession>(Builders<AuthSession>.IndexKeys.Ascending(s => s.UserId)),
                new CreateIndexModel<AuthSession>(Builders<AuthSession>.IndexKeys.Ascending(s => s.ExpiresUtc),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(7) }),
            ]);
        }
        catch (Exception ex) { Log.Warning("Session indexes couldn't be created: {Message}", ex.Message); }
    }

    public static string CleanDeviceName(string? name)
    {
        name = new string((name ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (name.Length == 0) return "Unknown device";
        return name.Length > 64 ? name[..64] : name;
    }

    // ---------------------------------------------------------------- device link (QR sign-in)

    private sealed class Link
    {
        public required string Id, UserId; public required DateTime ExpiresUtc;
        public string? RedeemedDevice;
    }
    private static readonly object LinkLock = new();
    private static readonly Dictionary<string, Link> LinksByHash = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Link> LinksById = new(StringComparer.Ordinal);
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(2);

    public sealed record DeviceLink(string Id, string Code, DateTime ExpiresUtc);

    /// <summary>A single-use code that signs a new device into <paramref name="userId"/>'s account. Only its hash is kept.</summary>
    public static DeviceLink CreateDeviceLink(string userId)
    {
        lock (LinkLock)
        {
            PruneLinks();
            if (LinksById.Values.Count(l => l.UserId == userId && l.RedeemedDevice == null) >= 5)
                throw new InvalidOperationException("Too many sign-in codes are active. Wait a couple of minutes.");
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var link = new Link { Id = Guid.NewGuid().ToString("N"), UserId = userId, ExpiresUtc = DateTime.UtcNow + LinkLifetime };
            LinksByHash[Hash(code)] = link;
            LinksById[link.Id] = link;
            return new(link.Id, code, link.ExpiresUtc);
        }
    }

    /// <summary>Redeems a device-link code: a new session on the new device, or null if the code is wrong, used or expired.</summary>
    public static Issued? RedeemDeviceLink(string? code, string? deviceName, string? address)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 32) return null;
        Link? link;
        lock (LinkLock)
        {
            var hash = Hash(code.ToUpperInvariant());
            if (!LinksByHash.Remove(hash, out link) || link.ExpiresUtc <= DateTime.UtcNow) return null;
            link.RedeemedDevice = CleanDeviceName(deviceName);
        }
        var user = Users.Find(u => u._id == link.UserId).FirstOrDefault();
        return user == null ? null : Start(user, deviceName, address);
    }

    /// <summary>For the device showing the code: whether it has been used yet, and by which device.</summary>
    public static (bool Found, bool Redeemed, string? Device, DateTime ExpiresUtc) DeviceLinkStatus(string id, string userId)
    {
        lock (LinkLock)
        {
            if (!LinksById.TryGetValue(id, out var l) || l.UserId != userId) return (false, false, null, default);
            return (true, l.RedeemedDevice != null, l.RedeemedDevice, l.ExpiresUtc);
        }
    }

    private static void PruneLinks()
    {
        var now = DateTime.UtcNow;
        foreach (var (h, l) in LinksByHash.Where(x => x.Value.ExpiresUtc <= now).ToArray()) LinksByHash.Remove(h);
        // Keep finished links a little longer so the showing device can still read "signed in on X".
        foreach (var (id, l) in LinksById.Where(x => x.Value.ExpiresUtc + TimeSpan.FromMinutes(5) <= now).ToArray()) LinksById.Remove(id);
    }
}
