using System.Security.Cryptography;
using System.Text;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Nova.Dsp;
using Serilog;

namespace Melon.LocalClasses;

/// <summary>
/// Saved equalizers ("EqFilters" collection). Each belongs to a user; the owner can make it public. A user can use
/// their own filters and public ones; only the owner edits, and the owner or an admin deletes.
/// </summary>
public static class EqFilters
{
    public const int MaxNameLength = 100;
    public const int MaxPerUser = 200;

    private static IMongoCollection<EqFilter> Collection =>
        StateManager.DbClient.GetDatabase("Melon").GetCollection<EqFilter>("EqFilters");

    public static void EnsureIndexes(IMongoDatabase db)
    {
        try
        {
            var keys = Builders<EqFilter>.IndexKeys;
            db.GetCollection<EqFilter>("EqFilters").Indexes.CreateMany(
            [
                new CreateIndexModel<EqFilter>(keys.Ascending(x => x.Owner), new CreateIndexOptions { Name = "owner" }),
                new CreateIndexModel<EqFilter>(keys.Ascending(x => x.IsPublic), new CreateIndexOptions { Name = "public" }),
            ]);
        }
        catch (Exception ex) { Log.Warning("EQ filter indexes couldn't be created: {Message}", ex.Message); }
    }

    /// <summary>The filter if <paramref name="userId"/> may use it (theirs or public), otherwise null.</summary>
    public static EqFilter? FindUsable(string id, string userId)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var filter = Collection.Find(f => f._id == id).FirstOrDefault();
        return filter != null && (filter.IsPublic || filter.Owner == userId) ? filter : null;
    }

    /// <summary>Filters the user can use: their own plus public ones (or only their own), sorted by name.</summary>
    public static List<ResponseEqFilter> List(string userId, bool mineOnly, string name, int page, int count)
    {
        var f = Builders<EqFilter>.Filter;
        var visible = mineOnly ? f.Eq(x => x.Owner, userId) : f.Or(f.Eq(x => x.Owner, userId), f.Eq(x => x.IsPublic, true));
        if (!string.IsNullOrWhiteSpace(name))
            visible &= f.Regex(x => x.Name, new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(name.Trim()), "i"));
        var filters = Collection.Find(visible).SortBy(x => x.Name).ThenBy(x => x._id)
            .Skip(Math.Max(0, page) * count).Limit(count).ToList();
        return WithOwnerNames(filters);
    }

    public static ResponseEqFilter ToResponse(EqFilter filter) => WithOwnerNames([filter])[0];

    private static List<ResponseEqFilter> WithOwnerNames(List<EqFilter> filters)
    {
        var owners = filters.Select(x => x.Owner).Distinct().ToArray();
        var names = StateManager.DbClient.GetDatabase("Melon").GetCollection<User>("Users")
            .Find(Builders<User>.Filter.In(u => u._id, owners)).Project(u => new { u._id, u.Username }).ToList()
            .ToDictionary(u => u._id, u => u.Username);
        return filters.Select(x => new ResponseEqFilter(x, names.GetValueOrDefault(x.Owner, ""))).ToList();
    }

    /// <summary>Creates a filter owned by <paramref name="userId"/> and returns its id.</summary>
    /// <exception cref="ArgumentException">Invalid name or settings, or the user has too many filters.</exception>
    public static string Add(string userId, EqFilterRequest request)
    {
        var (name, bands) = Check(request);
        if (Collection.CountDocuments(x => x.Owner == userId) >= MaxPerUser)
            throw new ArgumentException($"You can save at most {MaxPerUser} EQ filters.");
        var now = DateTime.UtcNow;
        var filter = new EqFilter
        {
            _id = ObjectId.GenerateNewId().ToString(),
            Name = name,
            Owner = userId,
            IsPublic = request.IsPublic ?? false,
            Preamp = request.Preamp,
            AutoHeadroom = request.AutoHeadroom,
            Bands = bands,
            Created = now,
            Updated = now,
        };
        Collection.InsertOne(filter);
        return filter._id;
    }

    /// <summary>Replaces the name, bands and gain of the owner's filter. False if it isn't theirs or doesn't exist.</summary>
    public static bool Update(string id, string userId, EqFilterRequest request)
    {
        var (name, bands) = Check(request);
        var update = Builders<EqFilter>.Update
            .Set(x => x.Name, name)
            .Set(x => x.Preamp, request.Preamp)
            .Set(x => x.AutoHeadroom, request.AutoHeadroom)
            .Set(x => x.Bands, bands)
            .Set(x => x.Updated, DateTime.UtcNow);
        if (request.IsPublic is bool isPublic) update = update.Set(x => x.IsPublic, isPublic);
        return Collection.UpdateOne(x => x._id == id && x.Owner == userId, update).MatchedCount != 0;
    }

    public static bool SetPublic(string id, string userId, bool isPublic) =>
        Collection.UpdateOne(x => x._id == id && x.Owner == userId,
            Builders<EqFilter>.Update.Set(x => x.IsPublic, isPublic).Set(x => x.Updated, DateTime.UtcNow)).MatchedCount != 0;

    /// <summary>Deletes the filter if the user owns it or is an admin.</summary>
    public static bool Delete(string id, string userId, bool isAdmin) =>
        (isAdmin ? Collection.DeleteOne(x => x._id == id) : Collection.DeleteOne(x => x._id == id && x.Owner == userId)).DeletedCount != 0;

    public static void DeleteOwnedBy(string userId)
    {
        try { Collection.DeleteMany(x => x.Owner == userId); }
        catch (Exception ex) { Log.Warning("EQ filters of deleted user {User} couldn't be removed: {Message}", userId, ex.Message); }
    }

    // ── Nova ─────────────────────────────────────────────────────────────

    /// <summary>The Nova settings for a filter.</summary>
    public static EqualizerSettings ToSettings(EqFilter filter) => ToSettings(filter.Preamp, filter.AutoHeadroom, filter.Bands);

    public static EqualizerSettings ToSettings(double preamp, bool autoHeadroom, IEnumerable<EqFilterBand>? bands) => new()
    {
        Preamp = preamp,
        AutoHeadroom = autoHeadroom,
        Bands = (bands ?? []).Select(b => new EqBand
        {
            Type = ParseType(b?.Type),
            Frequency = b?.Frequency ?? 0,
            Gain = b?.Gain ?? 0,
            Q = b?.Q ?? 0,
            Slope = b?.Slope ?? 12,
            Enabled = b?.Enabled ?? false,
        }).ToList(),
    };

    /// <summary>
    /// A short hash of what affects the sound (not the name or visibility), so caches of processed audio change
    /// when the filter is edited.
    /// </summary>
    public static string Fingerprint(EqualizerSettings s)
    {
        var text = new StringBuilder(FormattableString.Invariant($"{s.Preamp:R}|{s.AutoHeadroom}"));
        foreach (var b in s.Bands)
            text.Append(FormattableString.Invariant($"|{b.Type},{b.Frequency:R},{b.Gain:R},{b.Q:R},{b.Slope},{b.Enabled}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())), 0, 8).ToLowerInvariant();
    }

    private static EqBandType ParseType(string? type) =>
        Enum.TryParse<EqBandType>(type, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : (EqBandType)(-1);

    private static (string Name, List<EqFilterBand> Bands) Check(EqFilterRequest? request)
    {
        if (request == null) throw new ArgumentException("A filter is required.");
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) throw new ArgumentException("Name is required.");
        if (name.Length > MaxNameLength) throw new ArgumentException($"Name must be at most {MaxNameLength} characters.");
        if (request.Bands?.Any(b => b != null && !Enum.TryParse<EqBandType>(b.Type, true, out var t) | !Enum.IsDefined(t)) == true)
            throw new ArgumentException("Band type must be peak, lowShelf, highShelf, lowPass, highPass, bandPass or notch.");
        var error = ToSettings(request.Preamp, request.AutoHeadroom, request.Bands).Validate();
        if (error != null) throw new ArgumentException(error);
        // Store the canonical type names so every client sees the same spelling.
        var bands = (request.Bands ?? []).Select(b => new EqFilterBand
        {
            Type = Camel(ParseType(b.Type).ToString()),
            Frequency = b.Frequency,
            Gain = b.Gain,
            Q = b.Q,
            Slope = b.Slope,
            Enabled = b.Enabled,
        }).ToList();
        return (name, bands);
    }

    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];
}
