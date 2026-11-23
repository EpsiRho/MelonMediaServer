using Melon.Models;
using MongoDB.Driver;
using Serilog;

namespace Melon.LocalClasses;

/// <summary>
/// Rules for account names and roles. Usernames are unique ignoring case (login picks "the" user with a name, so two
/// accounts sharing one let a user lock out or impersonate another), and a unique index backs that up in the database.
/// </summary>
public static class UserAccounts
{
    public const int MinUsernameLength = 2, MaxUsernameLength = 32, MaxPasswordLength = 1024, MaxBioLength = 1000;
    public static readonly string[] Roles = ["Admin", "User", "Pass"];

    /// <summary>Case-insensitive comparison for username lookups and the unique index.</summary>
    public static readonly Collation IgnoreCase = new("en", strength: CollationStrength.Secondary);

    /// <summary>Null when <paramref name="username"/> is acceptable as a new name, otherwise the reason it isn't.</summary>
    public static string? CheckUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Username cannot be empty.";
        if (username != username.Trim()) return "Username cannot start or end with a space.";
        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
            return $"Username must be {MinUsernameLength}–{MaxUsernameLength} characters.";
        foreach (var c in username)
            if (!(char.IsLetterOrDigit(c) || c is ' ' or '.' or '_' or '-'))
                return "Username can contain letters, digits, spaces, '.', '_' and '-'.";
        return null;
    }

    /// <summary>Null when <paramref name="password"/> is acceptable, otherwise the reason it isn't.</summary>
    public static string? CheckPassword(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "Password cannot be empty.";
        if (password.Length > MaxPasswordLength) return $"Password cannot be longer than {MaxPasswordLength} characters.";
        return null;
    }

    public static bool IsRole(string? role) => role != null && Roles.Contains(role);

    /// <summary>Whether another account (not <paramref name="exceptId"/>) already has this name, ignoring case.</summary>
    public static bool UsernameTaken(IMongoCollection<User> users, string username, string? exceptId = null)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Username, username);
        if (exceptId != null) filter &= Builders<User>.Filter.Ne(u => u._id, exceptId);
        return users.Find(filter, new FindOptions { Collation = IgnoreCase }).Limit(1).Any();
    }

    /// <summary>
    /// Creates the case-insensitive unique index on <c>Username</c>. When existing accounts already share a name the index
    /// can't be built: the duplicates are logged so an administrator can rename them, and the check in the API still
    /// stops new ones.
    /// </summary>
    public static void EnsureUniqueUsernames(IMongoDatabase db)
    {
        var users = db.GetCollection<User>("Users");
        try
        {
            users.Indexes.CreateOne(new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Collation = IgnoreCase, Name = "Username_unique_ci" }));
        }
        catch (MongoException)
        {
            var dupes = users.Find(FilterDefinition<User>.Empty).Project(u => u.Username).ToList()
                .Where(n => n != null).GroupBy(n => n.ToLowerInvariant()).Where(g => g.Count() > 1).Select(g => string.Join(" / ", g));
            Log.Warning("Usernames are not unique ({Duplicates}); rename these accounts so the unique username index can be created.",
                string.Join("; ", dupes));
        }
    }
}
