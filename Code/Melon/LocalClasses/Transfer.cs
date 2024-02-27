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
using Melon.Classes;
using Melon.DisplayClasses;
using MelonLib.Parsers;
using Pastel;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using System.Threading;
using Amazon.Util.Internal;
using static Melon.LocalClasses.StateManager;
namespace Melon.LocalClasses
{
    public static class Transfer
    {
        public static void TransferUI()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var options = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("DbBackupOption"), ExportDb },
                    { StringsManager.GetString("DbLoadBackupOption"), ImportDbUI },
                    { StringsManager.GetString("PlaylistExportOption"), ExportPlaylistUI },
                    { StringsManager.GetString("PlaylistImportOption"), ImportPlaylistUI },
                };

            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu") });

                // Input
                var commands = new List<string>();
                foreach (var key in options.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)options[choice])();
            }
        }
        private static void ExportPlaylistUI()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var PlaylistsCollection = NewMelonDB.GetCollection<Playlist>("Playlists");

            bool error = false;
            string input = "";
            Playlist plst = new Playlist();
            while (true)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), StringsManager.GetString("PlaylistExportOption") });
                Console.WriteLine(StringsManager.GetString("ExportPlaylistIdRequest").Pastel(MelonColor.Text));
                if (error)
                {
                    Console.WriteLine(StringsManager.GetString("PlaylistNotFound").Pastel(MelonColor.Error));
                }
                Console.Write("> ");
                input = Console.ReadLine();

                plst = PlaylistsCollection.AsQueryable().Where(x => x._id == input).FirstOrDefault();
                if (plst != null)
                {
                    break;
                }

                error = true;
            }

            var commands = new List<string>()
            {
                ".m3u",
                ".pls",
                ".xml"
            };

            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), StringsManager.GetString("PlaylistExportOption") });
            Console.WriteLine(StringsManager.GetString("FileFormatSelection").Pastel(MelonColor.Text));
            var choice = MelonUI.OptionPicker(commands);

            List<Track> tracks = TracksCollection.AsQueryable().Where(x => plst.Tracks.Select(y => y._id).Contains(x._id)).ToList();

            string txt = "";
            if (choice == (".m3u"))
            {
                txt = PlaylistFormatConverter.ToM3U(plst.Name, tracks);
            }
            else if (choice == ".pls")
            {
                txt = PlaylistFormatConverter.ToPLS(tracks);
            }
            else if (choice == ".xml")
            {
                txt = PlaylistFormatConverter.ToXML(tracks);
            }

            if (!Directory.Exists($"{StateManager.melonPath}/Exports"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Exports");
            }

            if (!Directory.Exists($"{StateManager.melonPath}/Exports/Playlists"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Exports/Playlists");
            }

            if (txt == "")
            {
                Console.WriteLine(StringsManager.GetString("PlaylistExportError").Pastel(MelonColor.Error));
                Console.ReadKey();
                return;
            }

            File.WriteAllText($"{StateManager.melonPath}/Exports/Playlists/{plst.Name}{choice}", txt);
        }
        private static void ImportPlaylistUI()
        {
            var NewMelonDB = DbClient.GetDatabase("Melon");
            var UsersCollection = NewMelonDB.GetCollection<User>("Users");

            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), StringsManager.GetString("PlaylistImportOption") });
            Console.WriteLine(StringsManager.GetString("PlaylistUserRequest").Pastel(MelonColor.Text));

            var commands = new List<string>();
            foreach (var user in UsersCollection.AsQueryable())
            {
                commands.Add($"{user.Username}-{user._id}");
            }
            var choice = MelonUI.OptionPicker(commands);
            string userId = choice.Split("-")[1];


            bool error = false;
            bool isDir = false;
            string input = "";
            while (true) {
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), StringsManager.GetString("PlaylistImportOption") });
                Console.WriteLine(StringsManager.GetString("ImportPlaylistFileRequest").Pastel(MelonColor.Text));
                Console.WriteLine(StringsManager.GetString("ImportPlaylistControls").Pastel(MelonColor.Text));
                if (error)
                {
                    Console.WriteLine(StringsManager.GetString("FileNotFound").Pastel(MelonColor.Error));
                }
                Console.Write("> ");
                input = Console.ReadLine();

                input = input.Replace("\"", "");

                if (File.Exists(input))
                {
                    break;
                }
                else if (Directory.Exists(input))
                {
                    isDir = true;
                    break;
                }

                error = true;
            }

            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), "Import Playlist" });
            Console.WriteLine(StringsManager.GetString("ImportProgress").Pastel(MelonColor.Text));
            //MelonUI.ShowIndeterminateProgress();

            List<string> files = new List<string>();
            if (isDir)
            {
                files.AddRange(Directory.GetFiles(input));
            }
            else
            {
                files.Add(input);
            }

            int count = 1;
            int x = Console.CursorLeft;
            int y = Console.CursorTop;
            MelonUI.DisplayProgressBar(0, files.Count(), '#', '-');
            foreach (var file in files)
            {
                ImportPlaylist(file, userId);
                Console.CursorLeft = x;
                Console.CursorTop = y;
                MelonUI.DisplayProgressBar(count, files.Count(), '#', '-');
                count++;
            }


            //MelonUI.HideIndeterminateProgress();
            Thread.Sleep(200);
        }
        private static void ImportDbUI()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var options = new List<string>()
                {
                    { StateManager.StringsManager.GetString("BackNavigation") },
                };
            if (!Directory.Exists($"{StateManager.melonPath}/Exports/DbBackups"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Exports/DbBackups");
            }
            foreach (var dir in Directory.GetDirectories($"{StateManager.melonPath}/Exports/DbBackups/"))
            {
                options.Add(dir.Replace(StateManager.melonPath, ""));

            }

            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), StringsManager.GetString("DbLoadBackupOption") });
                Console.WriteLine(StringsManager.GetString("ImportBackupSelection").Pastel(MelonColor.Text));

                // Input
                var choice = MelonUI.OptionPicker(options);

                if (choice == StateManager.StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ImportDb(choice);
            }
        }
        private static void ExportDb()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var AlbumsCollection = NewMelonDB.GetCollection<Album>("Albums");
            var ArtistsCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var PlaylistsCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
            var CollectionsCollection = NewMelonDB.GetCollection<Collection>("Collections");
            var MetadataCollection = NewMelonDB.GetCollection<DbMetadata>("Metadata");
            var StatsCollection = NewMelonDB.GetCollection<PlayStat>("Stats");
            var UsersCollection = NewMelonDB.GetCollection<User>("Users");

            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), StringsManager.GetString("DbBackupOption") });
            ChecklistUI.SetChecklistItems(new[]
                {
                    "Tracks",
                    "Albums",
                    "Artists",
                    "Playlists",
                    "Collections",
                    "Metadata",
                    "Stats",
                    "Users",
                });
            ChecklistUI.ChecklistDislayToggle();

            if (!Directory.Exists($"{StateManager.melonPath}/Exports/DbBackups"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Exports/DbBackups");
            }

            string dt = DateTime.Now.ToString("dd mm yy-hh mm ss tt").Replace(" ", "");
            Directory.CreateDirectory($"{StateManager.melonPath}/Exports/DbBackups/{dt}");

            string json = "";
            try
            {
                json = JsonConvert.SerializeObject(TracksCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/tracks.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(0, true);

            try
            {
                json = JsonConvert.SerializeObject(AlbumsCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/albums.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(1, true);

            try
            {
                json = JsonConvert.SerializeObject(ArtistsCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/artists.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(2, true);

            try
            {
                json = JsonConvert.SerializeObject(PlaylistsCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/playlists.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(3, true);

            try
            {
                json = JsonConvert.SerializeObject(CollectionsCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/collections.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(4, true);

            try
            {
                json = JsonConvert.SerializeObject(MetadataCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/metadata.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(5, true);

            try
            {
                json = JsonConvert.SerializeObject(StatsCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/stats.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(6, true);

            try
            {
                json = JsonConvert.SerializeObject(UsersCollection.AsQueryable());
                File.WriteAllText($"{StateManager.melonPath}/Exports/DbBackups/{dt}/users.json", json);
            }
            catch (Exception)
            {

            }
            ChecklistUI.UpdateChecklist(7, true);

            ChecklistUI.ChecklistDislayToggle();

            Thread.Sleep(200);
            MelonUI.ClearConsole();
        }
        private static void ImportDb(string path)
        {
            try
            {
                var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
                var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
                var AlbumsCollection = NewMelonDB.GetCollection<Album>("Albums");
                var ArtistsCollection = NewMelonDB.GetCollection<Artist>("Artists");
                var PlaylistsCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
                var CollectionsCollection = NewMelonDB.GetCollection<Collection>("Collections");
                var MetadataCollection = NewMelonDB.GetCollection<DbMetadata>("Metadata");
                var StatsCollection = NewMelonDB.GetCollection<PlayStat>("Stats");
                var UsersCollection = NewMelonDB.GetCollection<User>("Users");

                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StringsManager.GetString("ImportExportMenu"), "Import Database" });
                ChecklistUI.SetChecklistItems(new[]
                    {
                    "Tracks",
                    "Albums",
                    "Artists",
                    "Playlists",
                    "Collections",
                    "Metadata",
                    "Stats",
                    "Users"
                });
                ChecklistUI.ChecklistDislayToggle();

                if (!Directory.Exists($"{StateManager.melonPath}/Exports/DbBackups/"))
                {
                    Directory.CreateDirectory($"{StateManager.melonPath}/Exports/DbBackups/");
                }

                string json = "";

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/tracks.json");
                    var tracks = JsonConvert.DeserializeObject<List<Track>>(json);
                    var trackModels = new List<WriteModel<Track>>();

                    foreach (var track in tracks)
                    {
                        var filter = Builders<Track>.Filter.Eq(t => t._id, track._id);
                        var update = Builders<Track>.Update.Set(t => t, track);

                        trackModels.Add(new ReplaceOneModel<Track>(filter, track) { IsUpsert = true });
                    }

                    if (trackModels.Count != 0)
                    {
                        TracksCollection.BulkWrite(trackModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(0, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/albums.json");
                    var albums = JsonConvert.DeserializeObject<List<Album>>(json);
                    var albumModels = new List<WriteModel<Album>>();

                    foreach (var album in albums)
                    {
                        var filter = Builders<Album>.Filter.Eq(a => a._id, album._id);
                        var update = Builders<Album>.Update.Set(a => a, album);

                        albumModels.Add(new ReplaceOneModel<Album>(filter, album) { IsUpsert = true });
                    }

                    if (albumModels.Count != 0)
                    {
                        AlbumsCollection.BulkWrite(albumModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(1, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/artists.json");
                    var artists = JsonConvert.DeserializeObject<List<Artist>>(json);
                    var artistModels = new List<WriteModel<Artist>>();

                    foreach (var artist in artists)
                    {
                        var filter = Builders<Artist>.Filter.Eq(a => a._id, artist._id);
                        var update = Builders<Artist>.Update.Set(a => a, artist);

                        artistModels.Add(new ReplaceOneModel<Artist>(filter, artist) { IsUpsert = true });
                    }

                    if (artistModels.Count != 0)
                    {
                        ArtistsCollection.BulkWrite(artistModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(2, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/playlists.json");
                    var playlists = JsonConvert.DeserializeObject<List<Playlist>>(json);
                    var playlistModels = new List<WriteModel<Playlist>>();

                    foreach (var playlist in playlists)
                    {
                        var filter = Builders<Playlist>.Filter.Eq(a => a._id, playlist._id);
                        var update = Builders<Playlist>.Update.Set(a => a, playlist);

                        playlistModels.Add(new ReplaceOneModel<Playlist>(filter, playlist) { IsUpsert = true });
                    }

                    if (playlistModels.Count != 0)
                    {
                        PlaylistsCollection.BulkWrite(playlistModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(3, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/collections.json");
                    var collections = JsonConvert.DeserializeObject<List<Collection>>(json);
                    var collectionModels = new List<WriteModel<Collection>>();

                    foreach (var collection in collections)
                    {
                        var filter = Builders<Collection>.Filter.Eq(a => a._id, collection._id);
                        var update = Builders<Collection>.Update.Set(a => a, collection);

                        collectionModels.Add(new ReplaceOneModel<Collection>(filter, collection) { IsUpsert = true });
                    }

                    if (collectionModels.Count != 0)
                    {
                        CollectionsCollection.BulkWrite(collectionModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(4, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/metadata.json");
                    var metadatas = JsonConvert.DeserializeObject<List<DbMetadata>>(json);
                    var metadataModels = new List<WriteModel<DbMetadata>>();

                    foreach (var metadata in metadatas)
                    {
                        var filter = Builders<DbMetadata>.Filter.Eq(a => a.Name, metadata.Name);
                        var update = Builders<DbMetadata>.Update.Set(a => a, metadata);

                        metadataModels.Add(new ReplaceOneModel<DbMetadata>(filter, metadata) { IsUpsert = true });
                    }

                    if (metadataModels.Count != 0)
                    {
                        MetadataCollection.BulkWrite(metadataModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(5, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/stats.json");
                    var stats = JsonConvert.DeserializeObject<List<PlayStat>>(json);
                    var statModels = new List<WriteModel<PlayStat>>();

                    foreach (var stat in stats)
                    {
                        var filter = Builders<PlayStat>.Filter.Eq(a => a._id, stat._id);
                        var update = Builders<PlayStat>.Update.Set(a => a, stat);

                        statModels.Add(new ReplaceOneModel<PlayStat>(filter, stat) { IsUpsert = true });
                    }

                    if (statModels.Count != 0)
                    {
                        StatsCollection.BulkWrite(statModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(6, true);

                try
                {
                    json = File.ReadAllText($"{StateManager.melonPath}/{path}/users.json");
                    var users = JsonConvert.DeserializeObject<List<User>>(json);
                    var userModels = new List<WriteModel<User>>();

                    foreach (var user in users)
                    {
                        var filter = Builders<User>.Filter.Eq(u => u._id, user._id);
                        var update = Builders<User>.Update.Set(u => u, user);

                        userModels.Add(new ReplaceOneModel<User>(filter, user) { IsUpsert = true });
                    }

                    if (userModels.Count != 0)
                    {
                        UsersCollection.BulkWrite(userModels);
                    }
                }
                catch (Exception)
                {

                }
                ChecklistUI.UpdateChecklist(7, true);

                ChecklistUI.ChecklistDislayToggle();

                Thread.Sleep(200);
                MelonUI.ClearConsole();
            }
            catch (Exception)
            {
                ChecklistUI.ChecklistDislayToggle();

                Thread.Sleep(200);
                MelonUI.ClearConsole();
            }
        }
        private static bool ImportPlaylist(string input, string user)
        {
            var NewMelonDB = DbClient.GetDatabase("Melon");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var PlaylistsCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
            KeyValuePair<string, List<string>> fileOut = KeyValuePair.Create("", new List<string>());
            if (input.EndsWith(".m3u") || input.EndsWith(".m3u8"))
            {
                fileOut = PlaylistFormatConverter.FromM3U(input);
            }
            else if (input.EndsWith(".pls"))
            {
                fileOut = PlaylistFormatConverter.FromPLS(input);
            }
            else if (input.EndsWith(".xml") || input.EndsWith(".xspf"))
            {
                fileOut = PlaylistFormatConverter.FromXML(input);
            }

            if (fileOut.Value.Count == 0)
            {
                return false;
            }
            
            var filter = Builders<Track>.Filter.In(x=>x.Path, fileOut.Value.Select(x=>x.Replace("\\","/")));
            var trackProjection = Builders<Track>.Projection.Include(x => x._id)
                                                            .Include(x => x.Name);
            var tracks = TracksCollection.Find(filter)
                                         .Project(trackProjection)
                                         .ToList()
                                         .Select(x => new DbLink() { _id = x["_id"].ToString(), Name = x["Name"].ToString() })
                                         .ToList();

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
            PlaylistsCollection.InsertOne(plst);

            return true;
        }
    }
}
