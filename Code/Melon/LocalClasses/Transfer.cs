using Melon.Models;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melon.LocalClasses;
using MelonLib.Parsers;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using System.Threading;
using static Melon.LocalClasses.StateManager;
using MongoDB.Bson.Serialization;
namespace Melon.LocalClasses
{
    public static class Transfer
    {
        private static readonly string[] BackupCollections = ["Tracks", "Albums", "Artists", "Playlists", "Collections", "Metadata", "Stats", "Users"];
        private static readonly string[] AdditionalBackupCollections = ["Queues", "FailedFiles"];
        private static string BackupDirectory => Path.Combine(melonPath, "Exports", "DbBackups");

        public static string[] ListBackups() => Directory.Exists(BackupDirectory)
            ? Directory.EnumerateDirectories(BackupDirectory).Select(Path.GetFileName).OfType<string>()
                .Where(name => !name.StartsWith('.')).OrderDescending().ToArray()
            : [];

        public static bool ExportDb()
        {
            var id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            var staging = Path.Combine(BackupDirectory, "." + id);
            Directory.CreateDirectory(staging);
            try
            {
                var db = DbClient.GetDatabase("Melon");
                foreach (var name in BackupCollections.Concat(AdditionalBackupCollections))
                {
                    var documents = db.GetCollection<BsonDocument>(name).Find(FilterDefinition<BsonDocument>.Empty).ToList();
                    File.WriteAllText(Path.Combine(staging, name.ToLowerInvariant() + ".json"), documents.ToJson());
                }
                Directory.Move(staging, Path.Combine(BackupDirectory, id));
                return true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Database backup failed");
                return false;
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }

        public static void ImportDb(string name)
        {
            if (!ListBackups().Contains(name)) throw new FileNotFoundException("Backup not found.");
            var db = DbClient.GetDatabase("Melon");
            // Older backups predate the queue and failed-file collections.
            var available = BackupCollections.Concat(AdditionalBackupCollections.Where(key =>
                File.Exists(Path.Combine(BackupDirectory, name, key.ToLowerInvariant() + ".json"))));
            var collections = available.ToDictionary(key => key, key =>
                BsonSerializer.Deserialize<List<BsonDocument>>(File.ReadAllText(Path.Combine(BackupDirectory, name, key.ToLowerInvariant() + ".json"))));
            foreach (var (collection, documents) in collections)
            {
                var writes = documents.Select(doc => new ReplaceOneModel<BsonDocument>(new BsonDocument("_id", doc["_id"]), doc)
                    { IsUpsert = true }).ToList();
                if (writes.Count > 0) db.GetCollection<BsonDocument>(collection).BulkWrite(writes);
            }
        }

        public static (string Name, string Contents) GetPlaylistExport(string id, string format, string? owner = null)
        {
            var db = DbClient.GetDatabase("Melon");
            var playlist = db.GetCollection<Playlist>("Playlists").Find(p => p._id == id).FirstOrDefault();
            if (playlist == null)
            {
                var collection = db.GetCollection<Collection>("Collections").Find(p => p._id == id).FirstOrDefault();
                if (collection != null) playlist = new Playlist { Name = collection.Name, Owner = collection.Owner, Tracks = collection.Tracks };
            }
            if (playlist == null || owner != null && playlist.Owner != owner) throw new FileNotFoundException("Playlist not found.");
            string name = Path.GetFileName(playlist.Name.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Invalid playlist file name.");
            var ids = playlist.Tracks.Select(track => track._id).ToArray();
            var found = db.GetCollection<Track>("Tracks").Find(Builders<Track>.Filter.In(t => t._id, ids))
                .ToList().ToDictionary(track => track._id);
            var tracks = ids.Where(found.ContainsKey).Select(id => found[id]).ToList();
            var contents = format switch
            {
                ".m3u" => PlaylistFormatConverter.ToM3U(playlist.Name, tracks),
                ".pls" => PlaylistFormatConverter.ToPLS(tracks),
                ".xml" => PlaylistFormatConverter.ToXML(tracks),
                _ => throw new ArgumentException("Unsupported playlist format.")
            };
            return (name + format, contents);
        }

        public static bool ImportPlaylist(string input, string user, bool collection = false)
        {
            var NewMelonDB = DbClient.GetDatabase("Melon");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var PlaylistsCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
            var fileOut = Path.GetExtension(input).ToLowerInvariant() switch
            {
                ".m3u" or ".m3u8" => PlaylistFormatConverter.FromM3U(input),
                ".pls" => PlaylistFormatConverter.FromPLS(input),
                ".xml" or ".xspf" => PlaylistFormatConverter.FromXML(input),
                _ => default
            };
            if (fileOut.Value == null || fileOut.Value.Count == 0) return false;

            var paths = fileOut.Value.Select(path => Path.GetFullPath(path, Path.GetDirectoryName(Path.GetFullPath(input))!).Replace('\\', '/')).ToArray();
            var matches = TracksCollection.Find(Builders<Track>.Filter.In(t => t.Path, paths))
                .Project(t => new { t.Path, t._id, t.Name }).ToList()
                .ToLookup(t => t.Path);
            var tracks = paths.Select(path => matches[path].FirstOrDefault()).Where(t => t != null)
                .Select(t => new DbLink { _id = t!._id, Name = t.Name }).ToList();

            Playlist plst = new Playlist();
            plst._id = ObjectId.GenerateNewId().ToString();
            plst.Name = fileOut.Key;
            plst.Description = $"{StringsManager.GetString("GenerationDescription")} {input.Split("/").Last()}";
            plst.Owner = user;
            plst.PublicEditing = false;
            plst.PublicViewing = false;
            plst.Editors = new List<string>();
            plst.Viewers = new List<string>();
            plst.ArtworkPath = "";
            plst.Tracks = tracks;
            plst.TrackCount = tracks.Count();
            if (collection)
                NewMelonDB.GetCollection<Collection>("Collections").InsertOne(new Collection
                {
                    _id = plst._id, Name = plst.Name, Owner = user, Description = plst.Description,
                    Tracks = tracks, TrackCount = tracks.Count, AndFilters = [], OrFilters = [],
                    ArtworkPath = "", Editors = [], Viewers = [], StaticMembership = true
                });
            else PlaylistsCollection.InsertOne(plst);

            return true;
        }
    }
}
