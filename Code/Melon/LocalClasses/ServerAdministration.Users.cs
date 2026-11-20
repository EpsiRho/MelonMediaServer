using Melon.Interface;
using Melon.Models;
using MongoDB.Driver;

namespace Melon.LocalClasses;

public sealed partial class ServerAdministration
{
    private static IMongoCollection<User> Users => Database.GetCollection<User>("Users");

    public ManagedUser[] GetUsers() => Users.Find(FilterDefinition<User>.Empty).SortBy(u => u.Username)
        .Project(u => new ManagedUser(u._id, u.Username, u.Type)).ToList().ToArray();

    public void SetUserType(string userId, string type)
    {
        if (type is not ("User" or "Admin"))
            throw new ArgumentException("Choose User or Admin.");

        lock (databaseLock)
        {
            var user = Users.Find(u => u._id == userId).Project(u => new { u.Type }).FirstOrDefault()
                ?? throw new ArgumentException("User not found.");
            if (user.Type == type) return;

            EnsureAnotherAdmin(userId, user.Type);
            var result = Users.UpdateOne(u => u._id == userId, Builders<User>.Update.Set(u => u.Type, type));
            if (result.MatchedCount == 0) throw new ArgumentException("User not found.");
            if (result.ModifiedCount != 0) Messages.Publish("users", "Account type changed.");
        }
    }

    public bool DeleteUser(string userId)
    {
        lock (databaseLock)
        {
            var user = Users.Find(u => u._id == userId).Project(u => new { u.Type }).FirstOrDefault();
            if (user == null) return false;
            EnsureAnotherAdmin(userId, user.Type);
            bool deleted = Users.DeleteOne(u => u._id == userId).DeletedCount != 0;
            if (deleted) Messages.Publish("users", "Account deleted.");
            return deleted;
        }
    }

    private static void EnsureAnotherAdmin(string userId, string currentType)
    {
        if (currentType == "Admin" && !Users.Find(u => u.Type == "Admin" && u._id != userId).Any())
            throw new InvalidOperationException("Keep at least one administrator.");
    }

    public AccessCode CreatePasswordReset(string userId)
    {
        var user = Users.Find(u => u._id == userId).Project(u => new { u.Password }).FirstOrDefault()
            ?? throw new ArgumentException("User not found.");
        return Security.IssueReset(userId, user.Password);
    }

    public bool ResetPassword(string code, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > 1024) throw new ArgumentException("Enter a password of up to 1024 characters.");
        return Security.RedeemReset(code, (id, currentHash) =>
        {
            var hash = Security.HashPassword(password, out var salt);
            return Users.UpdateOne(u => u._id == id && u.Password == currentHash,
                Builders<User>.Update.Set(u => u.Password, hash).Set(u => u.Salt, salt)).ModifiedCount == 1;
        });
    }

    public AccessCode CreateInvite(int minutes = 10) => Security.IssueInvite(TimeSpan.FromMinutes(minutes));
    public AccessCode[] GetInvites() => Security.ViewInviteCodes().OrderBy(x => x.Value).Select(x => new AccessCode(x.Key, x.Value)).ToArray();
    public void RevokeInvite(string code) => Security.InvalidateInviteCode(code);
}
