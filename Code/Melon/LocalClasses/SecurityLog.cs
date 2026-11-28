using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using Serilog.Events;

namespace Melon.LocalClasses;

/// <summary>
/// Records <see cref="SecurityEvent"/>s: to the server log, to the "SecurityEvents" collection (kept 90 days), and to
/// plugins. Recording never throws; a broken database only loses the stored copy.
/// </summary>
public static class SecurityLog
{
    public const int KeepDays = 90;
    private static readonly AsyncLocal<Origin?> CurrentOrigin = new();

    /// <summary>Who is acting and from where, for the rest of this request (set by the web API per request).</summary>
    public sealed record Origin(string? ActorId, string? Address);

    /// <summary>Sets the acting user and client address for events recorded later in this async flow.</summary>
    public static void SetOrigin(string? actorId, string? address) => CurrentOrigin.Value = new Origin(actorId, address);

    private static IMongoCollection<SecurityEvent>? Collection =>
        StateManager.DbClient?.GetDatabase("Melon").GetCollection<SecurityEvent>("SecurityEvents");

    private static readonly HashSet<string> WarningKinds =
    [
        SecurityEventKinds.SignInFailed, SecurityEventKinds.SignInThrottled, SecurityEventKinds.TokenReuse,
        SecurityEventKinds.PasswordChangeFailed, SecurityEventKinds.SignOutEveryone, SecurityEventKinds.OutsideRequestRefused,
        SecurityEventKinds.BlockedRequest,
    ];

    public static SecurityEvent Record(string kind, string? userId = null, string? username = null, string? sessionId = null,
        string? deviceName = null, string? detail = null, string? address = null)
    {
        var origin = CurrentOrigin.Value;
        var e = new SecurityEvent
        {
            _id = ObjectId.GenerateNewId().ToString(),
            TimeUtc = DateTime.UtcNow,
            Kind = kind,
            Warning = WarningKinds.Contains(kind),
            UserId = userId ?? "",
            Username = Clip(username, 64),
            ActorId = origin?.ActorId is { } a && a != userId ? a : "",
            Address = Clip(address ?? origin?.Address ?? "controller", 64),
            SessionId = sessionId ?? "",
            DeviceName = Clip(deviceName, 64),
            Detail = Clip(detail, 200),
        };

        Log.Write(e.Warning ? LogEventLevel.Warning : LogEventLevel.Information,
            "Security {Kind}: user {Username} ({UserId}) from {Address}{Detail}",
            e.Kind, e.Username, e.UserId, e.Address, e.Detail.Length > 0 ? " — " + e.Detail : "");
        try { Collection?.InsertOne(e); }
        catch (Exception ex) { Log.Warning("Security event couldn't be stored: {Message}", ex.Message); }
        try { (StateManager.WebApi as MWebApi)?.OnSecurityEvent(e); }
        catch (Exception ex) { Log.Warning(ex, "A plugin failed handling a security event."); }
        return e;
    }

    /// <summary>Newest first. <paramref name="userId"/> limits to one account; <paramref name="warningsOnly"/> to failures and alerts.</summary>
    public static SecurityEvent[] Recent(int count = 100, string? userId = null, bool warningsOnly = false, DateTime? sinceUtc = null)
    {
        var c = Collection;
        if (c == null) return [];
        var f = Builders<SecurityEvent>.Filter;
        var filter = f.Empty;
        if (!string.IsNullOrEmpty(userId)) filter &= f.Eq(x => x.UserId, userId);
        if (warningsOnly) filter &= f.Eq(x => x.Warning, true);
        if (sinceUtc is { } since) filter &= f.Gte(x => x.TimeUtc, since);
        try { return c.Find(filter).SortByDescending(x => x.TimeUtc).Limit(Math.Clamp(count, 1, 5000)).ToList().ToArray(); }
        catch (Exception ex) when (ex is MongoException or TimeoutException) { return []; }
    }

    public static void EnsureIndexes(IMongoDatabase db)
    {
        try
        {
            var keys = Builders<SecurityEvent>.IndexKeys;
            db.GetCollection<SecurityEvent>("SecurityEvents").Indexes.CreateMany(
            [
                new CreateIndexModel<SecurityEvent>(keys.Ascending(x => x.TimeUtc), new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(KeepDays), Name = "expire" }),
                new CreateIndexModel<SecurityEvent>(keys.Ascending(x => x.UserId).Descending(x => x.TimeUtc), new CreateIndexOptions { Name = "user_time" }),
            ]);
        }
        catch (Exception ex) { Log.Warning("Security event indexes couldn't be created: {Message}", ex.Message); }
    }

    private static string Clip(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var clean = new string(s.Where(ch => !char.IsControl(ch)).ToArray());
        return clean.Length <= max ? clean : clean[..max];
    }
}
