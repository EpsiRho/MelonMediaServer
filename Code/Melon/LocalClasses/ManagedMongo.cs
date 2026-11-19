using System.Security.Cryptography;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Melon.LocalClasses;

internal static class ManagedMongo
{
    internal const string Alias = "melon-managed-local";
    private const string Address = "127.0.0.1:27524";
    private const string FileName = "ManagedMongoCredentials";
    private static readonly string[] Secrets = [nameof(Credentials.Provisioner), nameof(Credentials.Application)];
    private sealed record Credentials { public string Provisioner { get; set; } = ""; public string Application { get; set; } = ""; }

    private static string Connection(string user, string password) =>
        $"mongodb://{user}:{password}@{Address}/Melon?authSource=admin&directConnection=true";

    internal static string SavedConnection()
    {
        var saved = Storage.LoadConfigFile<Credentials>(FileName, Secrets, out _) ?? throw new IOException("Managed MongoDB credentials are missing.");
        return Connection("melon-app", saved.Application);
    }

    internal static string Connect()
    {
        var saved = Storage.LoadConfigFile<Credentials>(FileName, Secrets, out _);
        if (saved == null)
        {
            if (File.Exists(Path.Combine(StateManager.melonPath, "Configs", FileName + ".json")))
                throw new IOException("Managed MongoDB credentials could not be read.");
            saved = new() { Provisioner = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Application = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) };
            Storage.SaveConfigFile(FileName, saved with { }, Secrets);
        }
        var appUri = Connection("melon-app", saved.Application);
        using var first = StateManager.CreateDatabaseClient($"mongodb://{Address}/?directConnection=true");
        try
        {
            first.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument {
                { "createUser", "melon-provisioner" }, { "pwd", saved.Provisioner },
                { "roles", new BsonArray { "userAdminAnyDatabase" } } });
        }
        catch (MongoCommandException ex) when (ex.Code == 13)
        {
            // The localhost exception closes after the first administrator is created.
        }
        using var manager = StateManager.CreateDatabaseClient(Connection("melon-provisioner", saved.Provisioner));
        var admin = manager.GetDatabase("admin");
        var users = admin.RunCommand<BsonDocument>(new BsonDocument("usersInfo", "melon-app"));
        if (users["users"].AsBsonArray.Count != 0) return appUri;
        admin.RunCommand<BsonDocument>(new BsonDocument {
            { "createUser", "melon-app" }, { "pwd", saved.Application }, { "roles", new BsonArray {
                new BsonDocument { { "role", "readWrite" }, { "db", "Melon" } },
                new BsonDocument { { "role", "dbAdmin" }, { "db", "Melon" } } } } });
        return appUri;
    }
}
