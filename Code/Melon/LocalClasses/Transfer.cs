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
        private static readonly string[] AdditionalBackupCollections = ["Queues", "FailedFiles", "EqFilters"];
        private static string BackupDirectory => Path.Combine(melonPath, "Exports", "DbBackups");

        // Uploaded and fetched artwork lives in these folders under the data directory, not in the database.
        private static readonly string[] ArtFolders = ["AlbumArts", "ArtistPfps", "ArtistBanners", "PlaylistArts", "CollectionArts", "UserPfps", "Assets"];
        // Art is kept once per distinct file (named by its SHA-256) and shared by every backup; each backup lists
        // its pictures in art.json. A library's art can be a gigabyte, so whole copies per backup would add up fast.
        private static string ArtStore => Path.Combine(BackupDirectory, ".art");
        private const string ArtManifest = "art.json";

        /// <summary>Adds the art folders to the shared store and writes the backup's art.json (folder/file → hash).</summary>
        private static void ExportArt(string staging)
        {
            Directory.CreateDirectory(ArtStore);
            var manifest = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var folder in ArtFolders)
            {
                var dir = Path.Combine(melonPath, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    try
                    {
                        string hash;
                        using (var stream = File.OpenRead(file))
                            hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
                        var blob = Path.Combine(ArtStore, hash);
                        if (!File.Exists(blob))
                        {
                            File.Copy(file, blob + ".part", true);
                            File.Move(blob + ".part", blob, true);
                        }
                        manifest[folder + "/" + Path.GetFileName(file)] = hash;
                    }
                    catch (IOException ex) { Serilog.Log.Warning(ex, "Backup skipped artwork {File}", file); }
                }
            }
            File.WriteAllText(Path.Combine(staging, ArtManifest), JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }

        /// <summary>Puts a backup's art back in place (files with the same name are overwritten; others are left alone).</summary>
        private static void ImportArt(string name)
        {
            var path = Path.Combine(BackupDirectory, name, ArtManifest);
            if (!File.Exists(path)) return; // backups from before art was included
            var manifest = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            foreach (var (entry, hash) in manifest)
            {
                var parts = entry.Split('/');
                // Only the known folders and plain file names: a tampered manifest can't write elsewhere.
                if (parts.Length != 2 || !ArtFolders.Contains(parts[0]) || parts[1] != Path.GetFileName(parts[1]) || parts[1] is "" or "." or ".."
                    || hash.Length != 64 || !hash.All(Uri.IsHexDigit)) continue;
                var blob = Path.Combine(ArtStore, hash);
                if (!File.Exists(blob)) { Serilog.Log.Warning("Backup {Backup} is missing artwork {Entry}", name, entry); continue; }
                var dir = Path.Combine(melonPath, parts[0]);
                Directory.CreateDirectory(dir);
                File.Copy(blob, Path.Combine(dir, parts[1]), true);
            }
        }

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
                ExportArt(staging);
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
            ImportArt(name);
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
