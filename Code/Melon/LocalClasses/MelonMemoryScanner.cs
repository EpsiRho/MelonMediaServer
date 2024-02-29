using Amazon.Util.Internal;
using Melon.Classes;
using Melon.DisplayClasses;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace Melon.LocalClasses
{
    /// <summary>
    /// Handles scanning local files and displaying scan progress.
    /// </summary>
    public static class MelonMemoryScanner
    {
        // Vars
        public static string CurrentFolder { get; set; }
        public static string CurrentFile { get; set; }
        public static string CurrentStatus { get; set; }
        public static double ScannedFiles { get; set; }
        public static double FoundFiles { get; set; }
        public static long averageMilliseconds { get; set; }
        public static bool Indexed { get; set; }
        public static bool endDisplay { get; set; }
        public static bool Scanning { get; set; }
        private static List<string> LyricFiles { get; set; }
        private static Stopwatch watch { get; set; }
        private static IMongoDatabase newMelonDB { get; set; }
        private static IMongoCollection<Artist> artistsCollection { get; set; }
        private static IMongoCollection<Album> albumsCollection { get; set; }
        private static IMongoCollection<Track> tracksCollection { get; set; }
        private static ConcurrentBag<ProtoTrack> tracks { get; set; }
        private static ConcurrentDictionary<string, string> threads { get; set; }

        // Main Scanning Functions
        public static void StartScan(object skipBool)
        {
            if (Scanning)
            {
                return;
            }

            Scanning = true;

            // Get MongoDB collections
            newMelonDB = StateManager.DbClient.GetDatabase("Melon");
            artistsCollection = newMelonDB.GetCollection<Artist>("Artists");
            albumsCollection = newMelonDB.GetCollection<Album>("Albums");
            tracksCollection = newMelonDB.GetCollection<Track>("Tracks");
            var DbTracks = tracksCollection.AsQueryable().ToList();
            tracks = new ConcurrentBag<ProtoTrack>(DbTracks.Select(x=>new ProtoTrack(x)));

            bool skip = (bool)skipBool;
            LyricFiles = new List<string>();
            watch = new Stopwatch();
            ScannedFiles = 1;
            FoundFiles = 1;
            averageMilliseconds = 1;
            Indexed = false;

            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CheckDbVersioningStatus");
            CheckAndFix();

            if(StateManager.MelonSettings.LibraryPaths.Count() == 0)
            {
                MelonUI.ClearConsole();
                Console.WriteLine(StateManager.StringsManager.GetString("NoPathWarning").Pastel(MelonColor.Error));
                Console.WriteLine(StateManager.StringsManager.GetString("ContinuationPrompt").Pastel(MelonColor.BackgroundText));
                Console.ReadKey(intercept: true);
                return;
            }

            if (!Directory.Exists($"{StateManager.melonPath}/AlbumArts"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/AlbumArts");
            }

            foreach (var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolderCounter(path);
            }

            threads = new ConcurrentDictionary<string, string>();
            foreach (var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolder(path, skip);
            }

            while (threads.Count != 0)
            {

            }



            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            FillOutDB();

            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("DeletionStepStatus");
            DeletePass();

            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CollectionStepStatus");
            UpdateCollections();

            endDisplay = true;
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CompletionStatus");
            IndexCollections();
            DisplayManager.UIExtensions.Clear();
            LyricFiles = null;
            Scanning = false;
        }
        private static void FillOutDB()
        {
            List<Track> dbTracks = new List<Track>();
            List<Album> dbAlbums = new List<Album>();
            List<Artist> dbArtists = new List<Artist>();
            var albums = tracks.Select(t=>t.Album).DistinctBy(a=>$"{a.Name} && {String.Join(";",a.AlbumArtists.Select(x=>x.Name))}").ToList();
            ScannedFiles = 0;
            FoundFiles = albums.Count();
            CurrentStatus = "Creating Albums";
            foreach (var album in albums)
            {
                var sTracks = tracks.Where(t => t.Album.Name == album.Name && t.Album.AlbumArtists.Select(a=>a.Name).Contains(album.AlbumArtists.FirstOrDefault().Name)).ToList();
                sTracks = sTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).ToList();
                var aArtists = sTracks.SelectMany(t => t.Album.AlbumArtists).DistinctBy(a => a.Name).Select(x => new DbLink() 
                { 
                    _id = dbArtists.Where(y=>y.Name == x.Name).FirstOrDefault() != null ? dbArtists.Where(y => y.Name == x.Name).FirstOrDefault()._id : ObjectId.GenerateNewId().ToString(), 
                    Name = x.Name 
                }).ToList();
                var cArtists = sTracks.SelectMany(t => t.Album.ContributingArtists).DistinctBy(a => a.Name).Select(x => new DbLink()
                {
                    _id = dbArtists.Where(y => y.Name == x.Name).FirstOrDefault() != null ? dbArtists.Where(y => y.Name == x.Name).FirstOrDefault()._id : ObjectId.GenerateNewId().ToString(),
                    Name = x.Name
                }).ToList();
                cArtists = cArtists.Where(x => !aArtists.Select(y => y.Name).Contains(x.Name)).ToList();

                var a = new Album()
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = album.Name,
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Bio = "",
                    TotalDiscs = album.TotalDiscs,
                    TotalTracks = album.TotalTracks,
                    Publisher = album.Publisher,
                    ReleaseStatus = album.ReleaseStatus,
                    ReleaseType = album.ReleaseType,
                    ReleaseDate = album.ReleaseDate,
                    AlbumArtists = aArtists,
                    AlbumArtPaths = new List<string>(),
                    Tracks = sTracks.Select(t=>new DbLink() { _id = t._id, Name = t.Name }).ToList(),
                    ContributingArtists = cArtists,
                    AlbumGenres = sTracks.SelectMany(t=>t.TrackGenres).Distinct().ToList(),
                    AlbumArtCount = 0,
                    AlbumArtDefault = 0,
                    PlayCounts = new List<UserStat>(),
                    Ratings = new List<UserStat>(),
                    SkipCounts = new List<UserStat>(),
                    ServerURL = ""
                };
                dbArtists.AddRange(aArtists.Select(x=> new Artist()
                {
                    _id = x._id,
                    Name = x.Name,
                    Bio = "",
                    Ratings = new List<UserStat>(),
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Releases = new List<DbLink>(),
                    Genres = new List<string>(),
                    SeenOn = new List<DbLink>(),
                    Tracks = new List<DbLink>(),
                    ConnectedArtists = new List<DbLink>(),
                    ArtistBannerArtCount = 0,
                    ArtistPfpArtCount = 0,
                    ArtistBannerArtDefault = 0,
                    ArtistPfpDefault = 0,
                    ArtistBannerPaths = new List<string>(),
                    ArtistPfpPaths = new List<string>(),
                    PlayCounts = new List<UserStat>(),
                    SkipCounts = new List<UserStat>(),
                    ServerURL = ""
                }));
                dbArtists.AddRange(cArtists.Select(x => new Artist()
                {
                    _id = x._id,
                    Name = x.Name,
                    Bio = "",
                    Ratings = new List<UserStat>(),
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Releases = new List<DbLink>(),
                    Genres = new List<string>(),
                    SeenOn = new List<DbLink>(),
                    Tracks = new List<DbLink>(),
                    ConnectedArtists = new List<DbLink>(),
                    ArtistBannerArtCount = 0,
                    ArtistPfpArtCount = 0,
                    ArtistBannerArtDefault = 0,
                    ArtistPfpDefault = 0,
                    ArtistBannerPaths = new List<string>(),
                    ArtistPfpPaths = new List<string>(),
                    PlayCounts = new List<UserStat>(),
                    SkipCounts = new List<UserStat>(),
                    ServerURL = ""
                }));
                dbAlbums.Add(a);
                Task up = new Task(() =>
                {
                    ScannedFiles++;
                });
                up.Start();
            }
            ScannedFiles = 0;
            FoundFiles = tracks.Count();
            CurrentStatus = "Updating Tracks";
            foreach (var track in tracks)
            {
                Track t = new Track(track);
                t.Album = dbAlbums.Where(x => x.Name == t.Album.Name).Select(x => new DbLink() { _id = x._id, Name = x.Name }).FirstOrDefault();
                for(int i = 0; i < t.TrackArtists.Count(); i++)
                {
                    t.TrackArtists[i] = dbArtists.Where(x => x.Name == t.TrackArtists[i].Name).Select(x=>new DbLink() { _id = x._id, Name = x.Name}).FirstOrDefault();
                }
                dbTracks.Add(t);
                Task up = new Task(() =>
                {
                    ScannedFiles++;
                });
                up.Start();
            }
            dbArtists = dbArtists.DistinctBy(x=>x.Name).ToList();
            ScannedFiles = 0;
            FoundFiles = dbArtists.Count();
            CurrentStatus = "Creating Artist";
            for (int i = 0; i < dbArtists.Count(); i++)
            {
                dbArtists[i].Releases = dbAlbums.Where(x => x.AlbumArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).OrderBy(x => x.ReleaseDate).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList();
                dbArtists[i].SeenOn = dbAlbums.Where(x => x.ContributingArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).OrderBy(x => x.ReleaseDate).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList();
                dbArtists[i].Tracks = dbTracks.Where(x => x.TrackArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).OrderBy(x => x.ReleaseDate).ThenBy(x => x.Disc).ThenBy(x => x.Position).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList();
                dbArtists[i].Genres = dbTracks.Where(x => x.TrackArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).SelectMany(x => x.TrackGenres).Distinct().ToList();
                Task up = new Task(() =>
                {
                    ScannedFiles++;
                });
                up.Start();
            }

            ScannedFiles = 0;
            FoundFiles = LyricFiles.Count();
            CurrentStatus = "Setting Lyrics";
            foreach (var lyricFile in LyricFiles)
            {
                var t = dbTracks.Where(x => x.Path.StartsWith(lyricFile.Replace(".lrc", "").Replace("\\", "/"))).FirstOrDefault();
                int idx = dbTracks.IndexOf(t);
                if (t != null)
                {
                    dbTracks[idx].LyricsPath = lyricFile;
                }
                Task up = new Task(() =>
                {
                    ScannedFiles++;
                });
                up.Start();
            }

            Upload(dbTracks, dbAlbums, dbArtists);
        }
        private static void Upload(List<Track> DbTracks, List<Album> DbAlbums, List<Artist> DbArtists)
        {
            //tracksCollection.InsertMany(tracks);
            var trackModels = new List<WriteModel<Track>>();

            foreach (var track in DbTracks)
            {
                var filter = Builders<Track>.Filter.Eq(a => a._id, track._id);
                var update = Builders<Track>.Update.Set(a => a, track);

                trackModels.Add(new ReplaceOneModel<Track>(filter, track) { IsUpsert = true });
            }

            if (trackModels.Count != 0)
            {
                tracksCollection.BulkWrite(trackModels);
            }

            //albumsCollection.InsertMany(albums);
            var albumModels = new List<WriteModel<Album>>();

            foreach (var album in DbAlbums)
            {
                var filter = Builders<Album>.Filter.Eq(a => a._id, album._id);
                var update = Builders<Album>.Update.Set(a => a, album);

                albumModels.Add(new ReplaceOneModel<Album>(filter, album) { IsUpsert = true });
            }

            if (albumModels.Count != 0)
            {
                albumsCollection.BulkWrite(albumModels);
            }

            //artistsCollection.InsertMany(artists);
            var artistModels = new List<WriteModel<Artist>>();

            foreach (var artist in DbArtists)
            {
                var filter = Builders<Artist>.Filter.Eq(a => a._id, artist._id);
                var update = Builders<Artist>.Update.Set(a => a, artist);

                artistModels.Add(new ReplaceOneModel<Artist>(filter, artist) { IsUpsert = true });
            }

            if (artistModels.Count != 0)
            {
                artistsCollection.BulkWrite(artistModels);
            }
        }
        public static void CheckAndFix()
        {
            var metadataCollection = newMelonDB.GetCollection<DbMetadata>("Metadata");
            var artistMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "ArtistsCollection").FirstOrDefault();
            var albumMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "AlbumsCollection").FirstOrDefault();
            var trackMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "TracksCollection").FirstOrDefault();
            var failedFilesMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "FailedFilesCollection").FirstOrDefault();
            var playlistsMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "PlaylistsCollection").FirstOrDefault();
            var collectionsMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "CollectionsCollection").FirstOrDefault();
            var queuesMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "QueuesCollection").FirstOrDefault();
            var usersMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "UsersCollection").FirstOrDefault();
            var statsMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "StatsCollection").FirstOrDefault();
            if (statsMetadata != null)
            {
                if (statsMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported User Database Version");
                }
            }
            else
            {
                var statMetadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "StatsCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(statMetadata);
            }

            if (usersMetadata != null)
            {
                if (usersMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported User Database Version");
                }
            }
            else
            {
                var userMetadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "UsersCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(userMetadata);
            }

            if (artistMetadata != null)
            {
                if (artistMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Artist Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "ArtistsCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (albumMetadata != null)
            {
                if (albumMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Album Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "AlbumsCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (trackMetadata != null)
            {
                if (trackMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Track Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "TracksCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (failedFilesMetadata != null)
            {
                if (failedFilesMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported FailedFiles Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "FailedFilesCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (playlistsMetadata != null)
            {
                if (playlistsMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Playlists Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "PlaylistsCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (collectionsMetadata != null)
            {
                if (collectionsMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Collections Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "CollectionsCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }

            if (queuesMetadata != null)
            {
                if (queuesMetadata.Version != "1.0.0")
                {
                    // Add code needed for upgrading db objects here
                    Serilog.Log.Error("Unsupported Queues Database Version");
                }
            }
            else
            {
                var metadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "QueuesCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(metadata);
            }
        }
        private static void DeletePass()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");

            int page = 0;
            int count = 100;

            ScannedFiles = 0;
            FoundFiles = tracks.Count();
            foreach(var track in tracks)
            {
                CurrentFile = track.Path;
                // Track is deleted, remove.
                if (!File.Exists(track.Path))
                {
                    // remove from albums
                    var filter = Builders<Album>.Filter.Eq(x=>x._id, track.Album._id);
                    var albums = AlbumCollection.Find(filter).ToList();

                    List<string> zeroed = new List<string>();
                    if(albums.Count() != 0)
                    {
                        var album = albums[0];
                        var query = (from t in album.Tracks
                                        where t._id == track._id
                                        select t).FirstOrDefault();
                        album.Tracks.Remove(query);
                        if (album.Tracks.Count == 0)
                        {
                            zeroed.Add(album._id);
                            AlbumCollection.DeleteOne(filter);
                        }
                        else
                        {
                            AlbumCollection.ReplaceOne(filter, album);
                        }
                    }

                    // remove from artists
                    foreach (var artist in track.TrackArtists)
                    {
                        var aFilter = Builders<Artist>.Filter.Eq(x=>x._id, artist._id);
                        var artists = ArtistCollection.Find(aFilter).ToList();
                        Artist dbArtist = null;

                        if(artists.Count() != 0)
                        {
                            dbArtist = artists[0];
                            var query = (from t in dbArtist.Tracks
                                            where t._id == track._id
                                            select t).FirstOrDefault();
                            dbArtist.Tracks.Remove(query);

                            if (dbArtist.Tracks.Count() == 0)
                            {
                                ArtistCollection.DeleteOne(aFilter);
                            }
                            else
                            {
                                foreach(var z in zeroed)
                                {
                                    var q = (from a in dbArtist.Releases
                                            where a._id == z
                                            select a).ToList();

                                    foreach(var a in q)
                                    {
                                        dbArtist.Releases.Remove(a);
                                    }

                                    q = (from a in dbArtist.SeenOn
                                            where a._id == z
                                            select a).ToList();

                                    foreach (var a in q)
                                    {
                                        dbArtist.SeenOn.Remove(a);
                                    }
                                }
                                ArtistCollection.ReplaceOne(aFilter, dbArtist);
                            }
                        }
                    }
                    // Remove Track
                    var tFilter = Builders<Track>.Filter.Eq(x=>x._id, track._id);
                    TracksCollection.DeleteOne(tFilter);
                }
                Task u = new Task(() =>
                {
                    ScannedFiles++;
                });
                u.Start();
            }
        }
        private static async void ScanFolderCounter(string path)
        {
            CurrentFolder = path;
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolderCounter(folder);
            }
            FoundFiles = FoundFiles + Directory.GetFiles(path).Count();
        }
        private static void ScanFolder(string path, bool skip)
        {
            // Recursively scan folders
            Task t = new Task(() =>
            {
                CurrentFolder = path;
            });
            t.Start();
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolder(folder, skip);
            }

            // Get Files, for each file:
            var files = Directory.GetFiles(path);
            foreach(var file in files)
            {
                while(threads.Count() > 50)
                {

                }
                Thread ft = new Thread(() =>
                {
                    threads.TryAdd(file, "");
                    watch.Restart();

                    try
                    {
                        Task f = new Task(() =>
                        {
                            CurrentFile = file;
                        });
                        f.Start();
                        var filename = Path.GetFileName(file);

                        // Lyrics matcher
                        if (filename.EndsWith(".lrc"))
                        {
                            LyricFiles.Add(file);
                            Task u = new Task(() =>
                            {
                                ScannedFiles++;
                            });
                            u.Start();
                            threads.TryRemove(file, out _);
                            return;
                        }

                        // Move on if file is not an Audio file
                        if (!IsAudioFile(file))
                        {
                            Task u = new Task(() =>
                            {
                                ScannedFiles++;
                            });
                            u.Start();
                            threads.TryRemove(file, out _);
                            return;
                        }

                        // Get the file metadata
                        var fileMetadata = new ATL.Track(file);

                        // Attempt to find the track if it's already in the DB
                        ProtoTrack trackDoc = tracks.Where(x => x.Path == file).FirstOrDefault();

                        Task s = new Task(() =>
                        {
                            CurrentStatus = StateManager.StringsManager.GetString("TagPreparationStatus");
                        });
                        s.Start();

                        if(trackDoc == null)
                        {
                            // Get and Split the artists metadata tag
                            List<string> albumArtists = SplitArtists(fileMetadata.AlbumArtist);
                            List<string> trackArtists = SplitArtists(fileMetadata.Artist);

                            // Split Genres
                            List<string> trackGenres = SplitGenres(fileMetadata.Genre);

                            // Conform Genres
                            // https://github.com/EpsiRho/MelonMediaServer/issues/12

                            // If artist name is nothing, set to "Unknown Album"
                            string albumName = string.IsNullOrEmpty(fileMetadata.Album) ? StateManager.StringsManager.GetString("UnknownAlbumStatus") : fileMetadata.Album;

                            trackDoc = new ProtoTrack()
                            {
                                _id = ObjectId.GenerateNewId().ToString(),
                                LastModified = File.GetLastWriteTime(fileMetadata.Path).ToUniversalTime(),
                                DateAdded = DateTime.UtcNow,
                                Name = fileMetadata.Title ?? StateManager.StringsManager.GetString("UnknownStatus"),
                                Album = new Album()
                                {
                                    _id = "",
                                    Name = albumName,
                                    DateAdded = DateTime.Now.ToUniversalTime(),
                                    Bio = "",
                                    TotalDiscs = fileMetadata.DiscTotal ?? 1,
                                    TotalTracks = fileMetadata.TrackTotal ?? 0,
                                    Publisher = fileMetadata.Publisher ?? "",
                                    ReleaseStatus = fileMetadata.AdditionalFields.TryGetValue("RELEASESTATUS", out var rs) ? rs : "",
                                    ReleaseType = fileMetadata.AdditionalFields.TryGetValue("RELEASETYPE", out var rt) ? rt : "",
                                    ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                                    AlbumArtists = albumArtists.Select(x => new DbLink() { Name = x }).ToList(),
                                    AlbumArtPaths = new List<string>(),
                                    Tracks = new List<DbLink>(),
                                    ContributingArtists = trackArtists.Select(x => new DbLink() { Name = x }).ToList(),
                                    AlbumGenres = trackGenres ?? new List<string>(),
                                    AlbumArtCount = 0,
                                    AlbumArtDefault = 0,
                                    PlayCounts = new List<UserStat>(),
                                    Ratings = new List<UserStat>(),
                                    SkipCounts = new List<UserStat>(),
                                    ServerURL = ""
                                },
                                Path = file.Replace("\\", "/"),
                                Position = fileMetadata.TrackNumber ?? 0,
                                Format = Path.GetExtension(fileMetadata.Path)?.TrimStart('.') ?? "",
                                Bitrate = fileMetadata.Bitrate.ToString() ?? "",
                                SampleRate = fileMetadata.SampleRate.ToString() ?? "",
                                Channels = fileMetadata.ChannelsArrangement?.NbChannels.ToString() ?? "",
                                BitsPerSample = fileMetadata.BitDepth.ToString() ?? "",
                                Disc = fileMetadata.DiscNumber ?? 1,
                                MusicBrainzID = fileMetadata.AdditionalFields.TryGetValue("MUSICBRAINZ_RELEASETRACKID", out var mbId) ? mbId : "",
                                ISRC = fileMetadata.AdditionalFields.TryGetValue("ISRC", out var isrc) ? isrc : "",
                                Year = fileMetadata.Year?.ToString() ?? "",
                                Ratings = new List<UserStat>(),
                                PlayCounts = new List<UserStat>(),
                                SkipCounts = new List<UserStat>(),
                                TrackArtCount = fileMetadata.EmbeddedPictures?.Count() ?? 0,
                                TrackArtDefault = 0,
                                Duration = fileMetadata.DurationMs.ToString() ?? "",
                                TrackArtists = trackArtists.Select(x=>new Artist() { Name = x}).ToList(),
                                TrackGenres = trackGenres ?? new List<string>(),
                                ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                                LyricsPath = "",
                                nextTrack = "",
                                ServerURL = "",
                            };

                            foreach(var a in trackArtists)
                            {
                                string name = string.IsNullOrEmpty(a) ? StateManager.StringsManager.GetString("UnknownArtistStatus") : a;
                                trackDoc.TrackArtists.Add(new Artist()
                                {
                                    Name = name
                                });
                                trackDoc.Album.ContributingArtists.Add(new DbLink()
                                {
                                    Name = name
                                });
                            }

                            foreach (var a in albumArtists)
                            {
                                string name = string.IsNullOrEmpty(a) ? StateManager.StringsManager.GetString("UnknownArtistStatus") : a;
                                trackDoc.Album.AlbumArtists.Add(new DbLink()
                                {
                                    Name = name
                                });
                            }
                        }
                        tracks.Add(trackDoc);
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("DuplicateKey"))
                        {
                            Task up = new Task(() =>
                            {
                                ScannedFiles++;
                            });
                            up.Start();
                            threads.TryRemove(file, out _);
                            return;
                        }

                        var FailedCollection = newMelonDB.GetCollection<FailedFile>("FailedFiles");

                        var failed = new FailedFile()
                        {
                            _id = ObjectId.GenerateNewId().ToString(),
                            Path = file,
                            ErrorMessage = e.Message,
                            StackTrace = e.StackTrace
                        };

                        FailedCollection.InsertOne(failed);
                    }
                    watch.Stop();
                    averageMilliseconds += watch.ElapsedMilliseconds;
                    Task update = new Task(() =>
                    {
                        ScannedFiles++;
                    });
                    update.Start();
                    threads.TryRemove(file, out _);
                    return;
                });
                ft.Start();

            };
            
        }

        private static bool IsAudioFile(string path)
        {
            var filename = Path.GetFileName(path);
            if (!filename.EndsWith(".flac") && !filename.EndsWith(".aac") && !filename.EndsWith(".wma") &&
                !filename.EndsWith(".wav") && !filename.EndsWith(".mp3") && !filename.EndsWith(".m4a"))
            {
                return false;
            }

            return true;
        }
        private static List<string> SplitArtists(string artistsStr)
        {
            // TODO: Allow changing the list of delimiters
            List<string> artists = new List<string>();
            var aSplit = artistsStr.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries);
            foreach (var a in aSplit)
            {
                string name = a;
                if(name == "")
                {
                    name = StateManager.StringsManager.GetString("UnknownArtistStatus");
                }
                if (!artists.Contains(name))
                {
                    artists.Add(name);
                }
            }

            return artists;
        }
        private static List<string> SplitGenres(string genresStr)
        {
            // TODO: Allow changing the list of delimiters
            List<string> genres = new List<string>();
            var gSplit = genresStr.Split(new string[] { ",", ";", "/" }, StringSplitOptions.TrimEntries);
            foreach (var g in gSplit)
            {
                if (g == "")
                {
                    continue;
                }
                if (!genres.Contains(g))
                {
                    genres.Add(g);
                }
            }

            genres.Remove("");

            return genres;
        }
        public static void UpdateCollections()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ColCollection = NewMelonDB.GetCollection<Collection>("Collections");
            int page = 0;
            int count = 100;
            FoundFiles = ColCollection.Count(Builders<Collection>.Filter.Empty);
            ScannedFiles = 0;
            while (true)
            {
                var collections = ColCollection.Find(Builders<Collection>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                // Sort the album's and artist's tracks and releases
                foreach (var collection in collections)
                {
                    var tracks = MelonAPI.FindTracks(collection.AndFilters, collection.OrFilters, collection.Owner);
                    if (tracks == null)
                    {
                        continue;
                    }
                    collection.Tracks = tracks;
                    collection.TrackCount = collection.Tracks.Count();
                    ColCollection.ReplaceOne(Builders<Collection>.Filter.Eq(x=>x._id, collection._id), collection);
                    ScannedFiles++;
                }

                page++;
                if (collections.Count() != 100)
                {
                    break;
                }
            }
        }
        public static void Sort()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            int page = 0;
            int count = 100;
            FoundFiles = AlbumCollection.Count(Builders<Album>.Filter.Empty);
            ScannedFiles = 0;
            while (true)
            {
                var albums = AlbumCollection.Find(Builders<Album>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                // Sort the album's and artist's tracks and releases
                foreach (var albumDoc in albums)
                {
                    try
                    {
                        var filter = Builders<Track>.Filter.In(a => a._id, albumDoc.Tracks.Select(x => x._id));
                        var fullTracks = TracksCollection.Find(filter).ToList();
                        albumDoc.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new DbLink() { _id = x._id, Name = x.Name }).ToList();
                        albumDoc.TotalTracks = albumDoc.Tracks.Count();
                        var albumFilter = Builders<Album>.Filter.Eq(x=>x._id, albumDoc._id);
                        AlbumCollection.ReplaceOneAsync(albumFilter, albumDoc);
                        ScannedFiles++;
                    }
                    catch (Exception)
                    {

                    }
                }

                page++;
                if (albums.Count() != 100)
                {
                    break;
                }
            }
            page = 0;
            FoundFiles = ArtistCollection.Count(Builders<Artist>.Filter.Empty);
            ScannedFiles = 0;
            while (true)
            {
                var artists = ArtistCollection.Find(Builders<Artist>.Filter.Empty).Skip(page * count).Limit(count).ToList();
                // Sort the album's and artist's tracks and releases
                foreach (var artistDoc in artists)
                {
                    var trackFilter = Builders<Track>.Filter.In(a => a._id, artistDoc.Tracks.Select(x => x._id));
                    var fullTracks = TracksCollection.Find(trackFilter).ToList();
                    var rAlbumFilter = Builders<Album>.Filter.In(a => a._id, artistDoc.Releases.Select(x => x._id));
                    var fullReleases = AlbumCollection.Find(rAlbumFilter).ToList();
                    var sAlbumFilter = Builders<Album>.Filter.In(a => a._id, artistDoc.SeenOn.Select(x => x._id));
                    var fullSeenOn = AlbumCollection.Find(sAlbumFilter).ToList();
                    try { artistDoc.Tracks = fullTracks.OrderBy(x=>x.ReleaseDate).ThenBy(x => x.Disc).ThenBy(x => x.Position).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList(); } catch (Exception) { }
                    try { artistDoc.Releases = fullReleases.OrderBy(x => x.ReleaseDate).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList(); } catch (Exception) { }
                    try { artistDoc.SeenOn = fullSeenOn.OrderBy(x => x.ReleaseDate).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList(); } catch (Exception) { }
                    var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, artistDoc._id);
                    ArtistCollection.ReplaceOneAsync(artistFilter, artistDoc);
                    ScannedFiles++;
                }

                page++;
                if (artists.Count() != 100)
                {
                    break;
                }
            }
        }
        private static void IndexCollections()
        {
            try
            {
                var indexOptions = new CreateIndexOptions { Background = true, Collation = new Collation(locale: "en", strength: CollationStrength.Secondary) };

                var artistIndexKeysDefinition = Builders<Artist>.IndexKeys.Ascending(x => x.Name);
                var artistIndexModel = new CreateIndexModel<Artist>(artistIndexKeysDefinition, indexOptions);
                artistsCollection.Indexes.CreateOne(artistIndexModel);

                var albumIndexKeysDefinition = Builders<Album>.IndexKeys.Ascending(x => x.Name);
                var albumIndexModel = new CreateIndexModel<Album>(albumIndexKeysDefinition, indexOptions);
                albumsCollection.Indexes.CreateOne(albumIndexModel);

                var trackIndexKeysDefinition = Builders<Track>.IndexKeys.Ascending(x => x.Name);
                var trackIndexModel = new CreateIndexModel<Track>(trackIndexKeysDefinition, indexOptions);
                tracksCollection.Indexes.CreateOne(trackIndexModel);
            }
            catch (Exception)
            {

            }
        }
        public static void ResetDb()
        {
            MelonUI.ShowIndeterminateProgress();
            newMelonDB = StateManager.DbClient.GetDatabase("Melon");

            var TracksCollection = newMelonDB.GetCollection<Track>("Tracks");
            TracksCollection.DeleteMany(Builders<Track>.Filter.Empty);
            TracksCollection.Indexes.DropAll();

            var ArtistCollection = newMelonDB.GetCollection<Artist>("Artists");
            ArtistCollection.DeleteMany(Builders<Artist>.Filter.Empty);
            ArtistCollection.Indexes.DropAll();

            var AlbumCollection = newMelonDB.GetCollection<Album>("Albums");
            AlbumCollection.DeleteMany(Builders<Album>.Filter.Empty);
            AlbumCollection.Indexes.DropAll();

            var QueueCollection = newMelonDB.GetCollection<PlayQueue>("Queues");
            QueueCollection.DeleteMany(Builders<PlayQueue>.Filter.Empty);

            var PlaylistCollection = newMelonDB.GetCollection<Playlist>("Playlists");
            PlaylistCollection.DeleteMany(Builders<Playlist>.Filter.Empty);

            var collectionCollection = newMelonDB.GetCollection<Collection>("Collections");
            collectionCollection.DeleteMany(Builders<Collection>.Filter.Empty);

            var failedCollection = newMelonDB.GetCollection<FailedFile>("FailedFiles");
            failedCollection.DeleteMany(Builders<FailedFile>.Filter.Empty);

            var metadataCollection = newMelonDB.GetCollection<DbMetadata>("Metadata");
            metadataCollection.DeleteMany(Builders<DbMetadata>.Filter.Empty);

            var statsCollection = newMelonDB.GetCollection<PlayStat>("Stats");
            statsCollection.DeleteMany(Builders<PlayStat>.Filter.Empty);

            if (Directory.Exists($"{StateManager.melonPath}/AlbumArts/"))
            {
                foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/AlbumArts/"))
                {
                    File.Delete(file);
                }
            }
            if (Directory.Exists($"{StateManager.melonPath}/ArtistBanners/"))
            {
                foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/ArtistBanners/"))
                {
                    File.Delete(file);
                }
            }
            if (Directory.Exists($"{StateManager.melonPath}/ArtistPfps/"))
            {
                foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/ArtistPfps/"))
                {
                    File.Delete(file);
                }
            }
            if (Directory.Exists($"{StateManager.melonPath}/Assets/"))
            {
                foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/Assets/"))
                {
                    File.Delete(file);
                }
            }
            if (Directory.Exists($"{StateManager.melonPath}/CollectionArts/"))
            {
                foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/CollectionArts/"))
                {
                    File.Delete(file);
                }
            }
            if (Directory.Exists($"{StateManager.melonPath}/PlaylistArts/"))
            {
                foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/PlaylistArts/"))
                {
                    File.Delete(file);
                }
            }

            MelonUI.HideIndeterminateProgress();
        }


        // UI
        public static void ResetDBUI()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("DatabaseMenu"), StateManager.StringsManager.GetString("DatabaseResetOption") });

            // Description
            Console.WriteLine(StateManager.StringsManager.GetString("DatabaseRemovalWarning").Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("RescanRequirement").Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("ResetConfirmation").Pastel(MelonColor.Text));
            string PositiveConfirmation = StateManager.StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StateManager.StringsManager.GetString("NegativeConfirmation");
            var input = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
            if (input == PositiveConfirmation) 
            {
                ResetDb();
            }
            else 
            {
                return;
            }
        }
        public static void Scan()
        {
            string PositiveConfirmation = StateManager.StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StateManager.StringsManager.GetString("NegativeConfirmation");
            if (Scanning)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("FullScanOption") });

                Console.WriteLine(StateManager.StringsManager.GetString("ScannerRunningCheck").Pastel(MelonColor.Text));
                var opt = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
                if(opt == PositiveConfirmation)
                {
                    ScanProgressView();
                }
                else
                {
                    return;
                }
            }
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("FullScanOption") });

            // Description
            Console.WriteLine(StateManager.StringsManager.GetString("FullLibraryScanInitiation").Pastel(MelonColor.Text));
            Console.WriteLine($"{StateManager.StringsManager.GetString("ScanDurationNote")} {StateManager.StringsManager.GetString("TimeConsumptionNote").Pastel(MelonColor.Highlight)} {StateManager.StringsManager.GetString("FileCountNote")}".Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("StartConfirmation").Pastel(MelonColor.Text));
            var input = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
            if (input == PositiveConfirmation)
            {
                if (!Scanning)
                {
                    Thread scanThread = new Thread(StartScan);
                    scanThread.Start(false);
                    DisplayManager.UIExtensions.Add(() => { Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                }
                ScanProgressView();
            }
            else
            {
                return;
            }
        }
        public static void ScanShort()
        {
            string PositiveConfirmation = StateManager.StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StateManager.StringsManager.GetString("NegativeConfirmation");
            if (Scanning)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("ShortScanOption") });

                Console.WriteLine(StateManager.StringsManager.GetString("ScannerRunningCheck").Pastel(MelonColor.Text));
                var opt = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
                if (opt == PositiveConfirmation)
                {
                    ScanProgressView();
                }
                else
                {
                    return;
                }
            }
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("ShortScanOption") });

            // Description
            Console.WriteLine(StateManager.StringsManager.GetString("ShortScanExplanation").Pastel(MelonColor.Text));
            Console.WriteLine($"{StateManager.StringsManager.GetString("ScanDurationNote")} {StateManager.StringsManager.GetString("TimeConsumptionNote").Pastel(MelonColor.Highlight)} {StateManager.StringsManager.GetString("FileCountNote")}".Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("StartConfirmation").Pastel(MelonColor.Text));
            var input = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
            if (input == PositiveConfirmation)
            {
                if(!Scanning)
                {
                    Thread scanThread = new Thread(StartScan);
                    scanThread.Start(true);
                    DisplayManager.UIExtensions.Add(() => { Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                    //DisplayManager.MenuOptions.Remove("Library Scanner");
                    //DisplayManager.MenuOptions.Insert(0, "Scan Progress", ScanProgressView);
                }
                ScanProgressView();
            }
            else 
            { 
                return;
            }
        }
        public static void ScanProgressView()
        {
            // Title
            Console.CursorVisible = false;
            MelonUI.ClearConsole();
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("ScannerProgressDisplay") });

            endDisplay = false;
            int sLeft = Console.CursorLeft;
            int sTop = Console.CursorTop;
            Thread DisplayThread = new Thread(() =>
            {
                int x = Console.WindowWidth;
                while (!endDisplay)
                {
                    if (endDisplay)
                    {
                        return;
                    }
                    if (x != Console.WindowWidth)
                    {
                        x = Console.WindowWidth;
                        MelonUI.ClearConsole();
                        MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("ScannerProgressDisplay") });
                    }
                    try
                    {
                        string controls = StateManager.StringsManager.GetString("BackEscapeControl");
                        int conX = Console.WindowWidth - controls.Length - 2;
                        Console.CursorLeft = conX;
                        Console.CursorTop = sTop;
                        Console.Write(controls.Pastel(MelonColor.BackgroundText));
                        Console.CursorTop = sTop;
                        Console.CursorLeft = sLeft;
                        Console.WriteLine($"{StateManager.StringsManager.GetString("ScanStatus")} {ScannedFiles.ToString().Pastel(MelonColor.Melon)} // {FoundFiles.ToString().Pastel(MelonColor.Melon)} {StateManager.StringsManager.GetString("FoundStatus")}");
                        MelonUI.DisplayProgressBar(ScannedFiles, FoundFiles, '#', '-');
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        string msg = $"{StateManager.StringsManager.GetString("TimeLeftDisplay")}: {TimeSpan.FromMilliseconds((averageMilliseconds / ScannedFiles)*(FoundFiles - ScannedFiles)).ToString(@"hh\:mm\:ss")}";
                        int max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"{StateManager.StringsManager.GetString("AverageFileTime")}: {TimeSpan.FromMilliseconds(averageMilliseconds / ScannedFiles)}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"{StateManager.StringsManager.GetString("FolderStatusDisplay")}: {CurrentFolder}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"{StateManager.StringsManager.GetString("FileStatusDisplay")}: {CurrentFile}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"{StateManager.StringsManager.GetString("SystemStatusDisplay")}: {CurrentStatus}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Threads: {threads.Count()}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                    }
                    catch (Exception)
                    {

                    }
                }
            });
            DisplayThread.Start();


            while (!endDisplay)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey();
                    if(k.Key == ConsoleKey.Escape)
                    {
                        endDisplay = true;
                        return;
                    }
                }
            }

        }
    }
}
