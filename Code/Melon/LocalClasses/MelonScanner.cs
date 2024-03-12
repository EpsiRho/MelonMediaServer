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
    public static class MelonScanner
    {
        // Vars
        public static string CurrentFolder { get; set; }
        public static string CurrentFile { get; set; }
        public static string CurrentStatus { get; set; }
        public static double ScannedFiles { get; set; }
        public static double FoundFiles { get; set; }
        public static long averageMilliseconds { get; set; }
        public static List<long> fileTimes { get; set; }
        public static bool Indexed { get; set; }
        public static bool endDisplay { get; set; }
        public static bool Scanning { get; set; }
        private static List<string> LyricFiles { get; set; }
        private static IMongoDatabase newMelonDB { get; set; }
        private static IMongoCollection<Artist> artistsCollection { get; set; }
        private static IMongoCollection<Album> albumsCollection { get; set; }
        private static IMongoCollection<Track> tracksCollection { get; set; }
        private static ConcurrentDictionary<string, ProtoTrack> tracks { get; set; }
        private static ConcurrentDictionary<string, Album> albums { get; set; }
        private static ConcurrentDictionary<string, Artist> artists { get; set; }
        private static ConcurrentDictionary<string, string> threads { get; set; }

        // DB Functions
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

        // Main Scanning Functions
        public static void StartScan(object skipBool)
        {
            // If currently scanning, don't start another scan.
            if (Scanning)
            {
                return;
            }
            Scanning = true;

            CurrentStatus = StateManager.StringsManager.GetString("LoadDbStatus");

            // Get MongoDB collections into memory
            newMelonDB = StateManager.DbClient.GetDatabase("Melon");
            artistsCollection = newMelonDB.GetCollection<Artist>("Artists");
            albumsCollection = newMelonDB.GetCollection<Album>("Albums");
            tracksCollection = newMelonDB.GetCollection<Track>("Tracks");
            var DbTracks = tracksCollection.AsQueryable().ToList();
            var DbAlbums = albumsCollection.AsQueryable().ToList();
            var DbArtists = artistsCollection.AsQueryable().ToList();
            tracks = new ConcurrentDictionary<string, ProtoTrack>(DbTracks.Select(x => KeyValuePair.Create(x.Path, new ProtoTrack(x))));
            albums = new ConcurrentDictionary<string, Album>(DbAlbums.Select(x => KeyValuePair.Create($"{x.Name} + {String.Join(",", x.AlbumArtists.Select(x => x.Name))}", x)));
            artists = new ConcurrentDictionary<string, Artist>(DbArtists.Select(x => KeyValuePair.Create($"{x.Name} + {x._id}", x)));
            foreach (var track in tracks)
            {
                track.Value.Album = albums.Values.Where(x => x._id == track.Value.Album._id).FirstOrDefault();
            }

            // Setup vars
            bool skip = (bool)skipBool;
            LyricFiles = new List<string>();
            ScannedFiles = 1; // Set to 1 to avoid divide by 0 errors
            FoundFiles = 1; // Set to 1 to avoid divide by 0 errors
            averageMilliseconds = 0;
            Indexed = false;
            endDisplay = false;
            fileTimes = new List<long>();

            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CheckDbVersioningStatus");

            // If Library Paths are empty, display a warning and return
            if (StateManager.MelonSettings.LibraryPaths.Count() == 0)
            {
                MelonUI.ClearConsole();
                DisplayManager.UIExtensions.Remove("LibraryScanIndicator");
                DisplayManager.UIExtensions.Add("LibraryScanError", () => 
                { 
                    Console.WriteLine(StateManager.StringsManager.GetString("NoPathWarning").Pastel(MelonColor.Error));
                    DisplayManager.UIExtensions.Remove("LibraryScanError");
                    MelonUI.endOptionsDisplay = true;
                });
                Scanning = false;
                endDisplay = true;
                return;
            }

            // Run through and get a count of how many files need scanning to display the proper number on the progressbar
            CurrentStatus = StateManager.StringsManager.GetString("ScannerFinding");
            foreach (var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolderCounter(path);
            }

            // Start recursive scan to open threads and load in file metadata 
            CurrentStatus = StateManager.StringsManager.GetString("ScannerScanning");
            threads = new ConcurrentDictionary<string, string>();
            foreach (var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolder(path, skip);
            }

            // After recursive function, wait until all opened threads have finished
            while (threads.Count != 0)
            {

            }

            // Fill out the database with the new tracks, creating artists and albums
            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            FillOutDB();

            // Delete any documents that should no longer exist
            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("DeletionStepStatus");
            DeletePass();

            // Update any collections with new track lists
            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CollectionStepStatus");
            UpdateCollections();

            // Set Indexes for mongodb
            endDisplay = true;
            FoundFiles = 100;
            ScannedFiles = 99;
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CompletionStatus");
            IndexCollections();

            // Cleanup
            DisplayManager.UIExtensions.Remove("LibraryScanIndicator");
            MelonUI.endOptionsDisplay = true;

            tracks = null;
            albums = null;
            artists = null;
            LyricFiles = null;
            Scanning = false;
            Thread.Sleep(500);

            if (OperatingSystem.IsWindows())
            {
                NotificationManager.ShowToast("Scanner", "Scanning is complete!");
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
            CurrentFolder = path;

            // Recursively scan folders
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolder(folder, skip);
            }

            // Get Files, for each file start a new thread to scan the file metadata into memory
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                // If the file path is already in the DB and scanner is running in fast mode, skip the file
                if (skip && tracks.ContainsKey(file.Replace("\\", "/")))
                {
                    ScannedFiles++;
                    continue;
                }

                // If there are 25 threads, don't add more threads till previous ones finish
                int workers = 0;
                int asyncIOThread = 0;
                ThreadPool.GetAvailableThreads(out workers, out asyncIOThread);
                while (threads.Count() >= 25)
                {
                    Thread.Sleep(100);
                }

                // Add thread to the thread tracker and start it
                Task.Factory.StartNew(() =>
                {
                    threads.TryAdd(file, "");
                    ScanInTrack(file);
                });
            };

        }
        private static void ScanInTrack(string file)
        {
            try
            {
                //CurrentFile = file;

                // Get the file name
                var filename = Path.GetFileName(file);

                // If this is a lyrics file, add it to the list and move on
                if (filename.EndsWith(".lrc"))
                {
                    LyricFiles.Add(file);
                    ScannedFiles++;
                    threads.TryRemove(file, out _);
                    return;
                }

                // Move on if the file is not an audio file
                if (!IsAudioFile(file))
                {
                    ScannedFiles++;
                    threads.TryRemove(file, out _);
                    return;
                }

                // Get the file metadata, in chunks of 200000 bytes
                ATL.Settings.FileBufferSize = 32768;
                var fileMetadata = new ATL.Track(file);

                //CurrentStatus = StateManager.StringsManager.GetString("TagPreparationStatus");

                // Get and Split the artists metadata tag
                List<string> albumArtists = SplitArtists(fileMetadata.AlbumArtist);
                List<string> trackArtists = SplitArtists(fileMetadata.Artist);

                // Split Genres
                List<string> trackGenres = SplitGenres(fileMetadata.Genre);

                // Conform Genres
                // https://github.com/EpsiRho/MelonMediaServer/issues/12

                // If album name is nothing, set to "Unknown Album"
                string albumName = string.IsNullOrEmpty(fileMetadata.Album) ? StateManager.StringsManager.GetString("UnknownAlbumStatus") : fileMetadata.Album;

                // Create a new ProtoTrack object and fill it with the file metadata
                var trackDoc = new ProtoTrack()
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
                    TrackArtists = trackArtists.Select(x => new Artist() { Name = x }).ToList(),
                    TrackGenres = trackGenres ?? new List<string>(),
                    ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                    LyricsPath = "",
                    nextTrack = "",
                    ServerURL = "",
                    Chapters = fileMetadata.Chapters != null ? fileMetadata.Chapters.Select(x => new Chapter()
                    {
                        _id = ObjectId.GenerateNewId().ToString(),
                        Title = x.Title,
                        Timestamp = x.UseOffset ? TimeSpan.FromMilliseconds(x.StartOffset) : TimeSpan.FromMilliseconds(x.StartTime),
                        Description = x.Subtitle,
                        Tracks = new List<DbLink>(),
                        Albums = new List<DbLink>(),
                        Artists = new List<DbLink>()
                    }).ToList() : new List<Chapter>()
                };

                // For each track artist, setup new artist doc
                // Also, setup contributing artists for the track's album
                foreach (var a in trackArtists)
                {
                    string name = string.IsNullOrEmpty(a) ? StateManager.StringsManager.GetString("UnknownArtistStatus") : a;
                    trackDoc.TrackArtists.Add(new Artist(name));
                    trackDoc.Album.ContributingArtists.Add(new DbLink()
                    {
                        Name = name
                    });
                }

                // For each album artist, add to track's album document
                foreach (var a in albumArtists)
                {
                    string name = string.IsNullOrEmpty(a) ? StateManager.StringsManager.GetString("UnknownArtistStatus") : a;
                    trackDoc.Album.AlbumArtists.Add(new DbLink()
                    {
                        Name = name
                    });
                }

                // Check if the track exists already, if it does then update it, otherwise insert the new track.
                if (tracks.ContainsKey(trackDoc.Path))
                {
                    trackDoc._id = tracks[trackDoc.Path]._id;
                    trackDoc.Album = new Album()
                    {
                        _id = "",
                        Name = albumName,
                        DateAdded = DateTime.Now.ToUniversalTime(),
                        Bio = "",
                        TotalDiscs = fileMetadata.DiscTotal ?? 1,
                        TotalTracks = fileMetadata.TrackTotal ?? 0,
                        Publisher = fileMetadata.Publisher ?? "",
                        ReleaseStatus = fileMetadata.AdditionalFields.TryGetValue("RELEASESTATUS", out var nrs) ? nrs : "",
                        ReleaseType = fileMetadata.AdditionalFields.TryGetValue("RELEASETYPE", out var nrt) ? nrt : "",
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
                    };
                    trackDoc.Ratings = tracks[trackDoc.Path].Ratings;
                    trackDoc.PlayCounts = tracks[trackDoc.Path].PlayCounts;
                    trackDoc.SkipCounts = tracks[trackDoc.Path].SkipCounts;
                    trackDoc.TrackArtDefault = tracks[trackDoc.Path].TrackArtDefault;
                    trackDoc.nextTrack = tracks[trackDoc.Path].nextTrack;
                    tracks[trackDoc.Path] = trackDoc;
                }
                else
                {
                    tracks.TryAdd(trackDoc.Path, trackDoc);
                }
            }
            catch (Exception e)
            {
                // If something fails, log the track, its error and stack trace to the failed files collection
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

            // Track added successfully, so update metrics and remove the thread from the tracker
            ScannedFiles++;
            threads.TryRemove(file, out _);
            return;
        }
        private static void FillOutDB()
        {
            List<Track> dbTracks = new List<Track>();
            List<Album> dbAlbums = new List<Album>();
            List<Artist> dbArtists = new List<Artist>();

            // Get Albums from found tracks (and old tracks, if applicable)
            var tempAlbums = tracks.Values.Select(t => t.Album).DistinctBy(a => $"{a.Name} && {String.Join(";", a.AlbumArtists.Select(x => x.Name))}").ToList();
            
            // Start creating albums
            ScannedFiles = 1;
            FoundFiles = albums.Count;
            CurrentStatus = StateManager.StringsManager.GetString("CreateAlbumsStatus");
            foreach (var album in tempAlbums)
            {
                // Get the first album artist name and use it to find tracks from this album/artist
                string artistName = album.AlbumArtists.Select(x => x.Name).FirstOrDefault();
                var sTracks = tracks.Values.Where(t => t.Album.Name == album.Name && t.Album.AlbumArtists.Select(a => a.Name).Contains(artistName)).ToList();
                sTracks = sTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).ToList();

                // Get the album artists and contributing artists
                var aArtists = sTracks.SelectMany(t => t.Album.AlbumArtists).DistinctBy(a => a.Name).Select(x => new DbLink()
                {
                    _id = artists.Values.FirstOrDefault(y => y.Name == x.Name) != null ? artists.Values.FirstOrDefault(y => y.Name == x.Name)._id : ObjectId.GenerateNewId().ToString(),
                    Name = x.Name
                }).ToList();
                var cArtists = sTracks.SelectMany(t => t.Album.ContributingArtists).DistinctBy(a => a.Name).Select(x => new DbLink()
                {
                    _id = artists.Values.FirstOrDefault(y => y.Name == x.Name) != null ? artists.Values.FirstOrDefault(y => y.Name == x.Name)._id : ObjectId.GenerateNewId().ToString(),
                    Name = x.Name
                }).ToList();

                // Remove duplicates
                cArtists = cArtists.Where(x => !aArtists.Select(y => y.Name).Contains(x.Name)).ToList();

                // Check if the album exists, if it does, update it, otherwise create a new album
                var foundAlbum = albums.Values.FirstOrDefault(x => x.Name == album.Name && x.AlbumArtists.Select(x => x.Name).Contains(artistName));
                if (foundAlbum == null)
                {
                    var a = new Album()
                    {
                        _id = ObjectId.GenerateNewId().ToString(),
                        Name = album.Name,
                        DateAdded = DateTime.Now.ToUniversalTime(),
                        Bio = "",
                        TotalDiscs = album.TotalDiscs,
                        TotalTracks = sTracks.Count,
                        Publisher = album.Publisher,
                        ReleaseStatus = album.ReleaseStatus,
                        ReleaseType = album.ReleaseType,
                        ReleaseDate = album.ReleaseDate,
                        AlbumArtists = aArtists,
                        AlbumArtPaths = new List<string>(),
                        Tracks = sTracks.Select(t => new DbLink() { _id = t._id, Name = t.Name }).ToList(),
                        ContributingArtists = cArtists,
                        AlbumGenres = sTracks.SelectMany(t => t.TrackGenres).Distinct().ToList(),
                        AlbumArtCount = 0,
                        AlbumArtDefault = 0,
                        PlayCounts = new List<UserStat>(),
                        Ratings = new List<UserStat>(),
                        SkipCounts = new List<UserStat>(),
                        ServerURL = ""
                    };
                    dbAlbums.Add(a);
                }
                else
                {
                    var a = new Album()
                    {
                        _id = foundAlbum._id,
                        Name = foundAlbum.Name,
                        DateAdded = DateTime.Now.ToUniversalTime(),
                        Bio = foundAlbum.Bio,
                        TotalDiscs = album.TotalDiscs,
                        TotalTracks = sTracks.Count,
                        Publisher = album.Publisher,
                        ReleaseStatus = album.ReleaseStatus,
                        ReleaseType = album.ReleaseType,
                        ReleaseDate = album.ReleaseDate,
                        AlbumArtists = aArtists,
                        AlbumArtPaths = foundAlbum.AlbumArtPaths,
                        Tracks = sTracks.Select(t => new DbLink() { _id = t._id, Name = t.Name }).ToList(),
                        ContributingArtists = cArtists,
                        AlbumGenres = sTracks.SelectMany(t => t.TrackGenres).Distinct().ToList(),
                        AlbumArtCount = foundAlbum.AlbumArtCount,
                        AlbumArtDefault = foundAlbum.AlbumArtDefault,
                        PlayCounts = foundAlbum.PlayCounts,
                        Ratings = foundAlbum.Ratings,
                        SkipCounts = foundAlbum.SkipCounts,
                        ServerURL = ""
                    };
                    dbAlbums.Add(a);
                }

                // Add artists to temp artist array for later.
                dbArtists.AddRange(aArtists.Select(x => new Artist()
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
                ScannedFiles++;
            }

            // Check if tempAlbums exist already, update if they do, add new if they don't.
            for (int i = 0; i < dbAlbums.Count; i++)
            {
                string n = $"{dbAlbums[i].Name} + {String.Join(",", dbAlbums[i].AlbumArtists.Select(x => x.Name))}";
                bool check = albums.ContainsKey(n);
                if (check)
                {
                    albums[n].AlbumArtists = dbAlbums[i].AlbumArtists;
                    albums[n].AlbumGenres = dbAlbums[i].AlbumGenres;
                    albums[n].Tracks = dbAlbums[i].Tracks;
                    albums[n].AlbumGenres = dbAlbums[i].AlbumGenres;
                    albums[n].ContributingArtists = dbAlbums[i].ContributingArtists;
                    albums[n].Publisher = dbAlbums[i].Publisher;
                    albums[n].ReleaseStatus = dbAlbums[i].ReleaseStatus;
                    albums[n].ReleaseType = dbAlbums[i].ReleaseType;
                    albums[n].TotalDiscs = dbAlbums[i].TotalDiscs;
                    albums[n].TotalTracks = dbAlbums[i].TotalTracks;
                    albums[n].ReleaseDate = dbAlbums[i].ReleaseDate;
                }
                else
                {
                    albums.TryAdd($"{dbAlbums[i].Name} + {String.Join(",", dbAlbums[i].AlbumArtists.Select(x => x.Name))}", dbAlbums[i]);
                }
                ScannedFiles++;
            }

            // Get distinct artists
            dbArtists = dbArtists.DistinctBy(x => x.Name).ToList();

            // Start creating artists
            ScannedFiles = 1;
            FoundFiles = dbArtists.Count;
            CurrentStatus = StateManager.StringsManager.GetString("CreateArtistsStatus");
            for (int i = 0; i < dbArtists.Count(); i++)
            {
                // Set artist info
                dbArtists[i].Releases = dbAlbums.Where(x => x.AlbumArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).OrderBy(x => x.ReleaseDate).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList();
                dbArtists[i].SeenOn = dbAlbums.Where(x => x.ContributingArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).OrderBy(x => x.ReleaseDate).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList();
                dbArtists[i].Tracks = tracks.Values.Where(x => x.TrackArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).OrderBy(x => x.ReleaseDate).ThenBy(x => x.Disc).ThenBy(x => x.Position).Select(x => new DbLink() { _id = x._id, Name = x.Name }).ToList();
                dbArtists[i].Genres = tracks.Values.Where(x => x.TrackArtists.Select(a => a.Name).Contains(dbArtists[i].Name)).SelectMany(x => x.TrackGenres).Distinct().ToList();
                
                // Try to add the artist, if you can't then update the artist
                bool check = artists.TryAdd($"{dbArtists[i].Name} + {dbArtists[i]._id}", dbArtists[i]);
                if (!check)
                {
                    artists[$"{dbArtists[i].Name} + {dbArtists[i]._id}"].Releases = dbArtists[i].Releases;
                    artists[$"{dbArtists[i].Name} + {dbArtists[i]._id}"].SeenOn = dbArtists[i].SeenOn;
                    artists[$"{dbArtists[i].Name} + {dbArtists[i]._id}"].Tracks = dbArtists[i].Tracks;
                    artists[$"{dbArtists[i].Name} + {dbArtists[i]._id}"].Genres = dbArtists[i].Genres;
                }
                ScannedFiles++;
            }

            // Update tracks with the proper album and artist ids
            ScannedFiles = 1;
            FoundFiles = tracks.Count;
            CurrentStatus = StateManager.StringsManager.GetString("UpdateTracksStatus");
            foreach (var track in tracks.Values)
            {
                Track t = new Track(track);
                t.Album = dbAlbums.Select(x => new DbLink() { _id = x._id, Name = x.Name }).FirstOrDefault(x => x.Name == t.Album.Name);
                for (int i = 0; i < t.TrackArtists.Count(); i++)
                {
                    t.TrackArtists[i] = artists.Values.Where(x => x.Name == t.TrackArtists[i].Name).Select(x => new DbLink() { _id = x._id, Name = x.Name }).FirstOrDefault();
                }
                t.TrackArtists = t.TrackArtists.DistinctBy(x => x._id).ToList();
                dbTracks.Add(t);
                ScannedFiles++;
            }

            // Update tracks to include found lyric files
            ScannedFiles = 1;
            FoundFiles = LyricFiles.Count;
            CurrentStatus = StateManager.StringsManager.GetString("UpdateLyricsStatus");
            foreach (var lyricFile in LyricFiles)
            {
                var t = dbTracks.Where(x => x.Path.StartsWith(lyricFile.Replace(".lrc", "").Replace("\\", "/"))).FirstOrDefault();
                int idx = dbTracks.IndexOf(t);
                if (t != null)
                {
                    dbTracks[idx].LyricsPath = lyricFile;
                }
                ScannedFiles++;
            }

            // Bulk write to MongoDB
            Upload(dbTracks, albums.Values.ToList(), artists.Values.ToList());
        }
        private static void Upload(List<Track> DbTracks, List<Album> DbAlbums, List<Artist> DbArtists)
        {
            // Update Tracks
            ScannedFiles = 1;
            FoundFiles = 4;
            CurrentStatus = StateManager.StringsManager.GetString("WritingTracksStatus");

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

            // Update Albums
            ScannedFiles = 2;
            CurrentStatus = StateManager.StringsManager.GetString("WritingAlbumsStatus");

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

            // Update Artists
            ScannedFiles = 3;
            CurrentStatus = StateManager.StringsManager.GetString("WritingArtistsStatus");

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

            ScannedFiles = 4;
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
            foreach (var track in tracks.Values)
            {
                CurrentFile = track.Path;
                // Track is deleted, remove.
                if (!File.Exists(track.Path))
                {
                    // remove from albums
                    var filter = Builders<Album>.Filter.Eq(x => x._id, track.Album._id);
                    var albums = AlbumCollection.Find(filter).ToList();

                    List<string> zeroed = new List<string>();
                    if (albums.Count() != 0)
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
                        var aFilter = Builders<Artist>.Filter.Eq(x => x._id, artist._id);
                        var artists = ArtistCollection.Find(aFilter).ToList();
                        Artist dbArtist = null;

                        if (artists.Count() != 0)
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
                                foreach (var z in zeroed)
                                {
                                    var q = (from a in dbArtist.Releases
                                             where a._id == z
                                             select a).ToList();

                                    foreach (var a in q)
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
                    var tFilter = Builders<Track>.Filter.Eq(x => x._id, track._id);
                    TracksCollection.DeleteOne(tFilter);
                }
                Task u = new Task(() =>
                {
                    ScannedFiles++;
                });
                u.Start();
            }
        }
        public static void UpdateCollections()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ColCollection = NewMelonDB.GetCollection<Collection>("Collections");
            int page = 0;
            int count = 100;
            FoundFiles = ColCollection.Count(Builders<Collection>.Filter.Empty);
            ScannedFiles = 1;
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
                    ColCollection.ReplaceOne(Builders<Collection>.Filter.Eq(x => x._id, collection._id), collection);
                    ScannedFiles++;
                }

                page++;
                if (collections.Count() != 100)
                {
                    break;
                }
            }
        }

        // Utility Functions
        private static bool IsAudioFile(string path)
        {
            var filename = Path.GetFileName(path);
            if (!filename.EndsWith(".flac") && !filename.EndsWith(".aac") && !filename.EndsWith(".wma") &&
                !filename.EndsWith(".wav") && !filename.EndsWith(".mp3") && !filename.EndsWith(".m4a") && !filename.EndsWith(".ogg"))
            {
                return false;
            }

            return true;
        }
        private static List<string> SplitArtists(string artistsStr)
        {
            // TODO: Allow changing the list of delimiters
            HashSet<string> artists = new HashSet<string>();
            var aSplit = artistsStr.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries);
            foreach (var a in aSplit)
            {
                string name = a;
                if (name == "")
                {
                    name = StateManager.StringsManager.GetString("UnknownArtistStatus");
                }
                artists.Add(name);
            }

            return artists.ToList();
        }
        private static List<string> SplitGenres(string genresStr)
        {
            // TODO: Allow changing the list of delimiters
            HashSet<string> genres = new HashSet<string>();
            var gSplit = genresStr.Split(new string[] { ",", ";", "/" }, StringSplitOptions.TrimEntries);
            foreach (var name in gSplit)
            {
                if (name == "")
                {
                    continue;
                }
                genres.Add(name);
            }

            return genres.ToList();
        }


        // UI
        public static void ResetDBUI()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("SettingsOption"), StateManager.StringsManager.GetString("DatabaseMenu"), StateManager.StringsManager.GetString("DatabaseResetOption") });

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
        public static void MemoryScan()
        {
            string PositiveConfirmation = StateManager.StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StateManager.StringsManager.GetString("NegativeConfirmation");
            if (Scanning)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), $"{StateManager.StringsManager.GetString("FullScanOption")} V2.0.0" });

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
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), $"{StateManager.StringsManager.GetString("FullScanOption")} V2.0.0" });

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
                    DisplayManager.UIExtensions.Add("LibraryScanIndicator", () => { Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight)); });
                    MelonUI.endOptionsDisplay = true;
                }
                ScanProgressView();
            }
            else
            {
                return;
            }
        }
        public static void MemoryScanShort()
        {
            string PositiveConfirmation = StateManager.StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StateManager.StringsManager.GetString("NegativeConfirmation");
            if (Scanning)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), $"{StateManager.StringsManager.GetString("ShortScanOption")} V2.0.0" });

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
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), $"{StateManager.StringsManager.GetString("ShortScanOption")} V2.0.0" });

            // Description
            Console.WriteLine(StateManager.StringsManager.GetString("ShortScanExplanation").Pastel(MelonColor.Text));
            Console.WriteLine($"{StateManager.StringsManager.GetString("ScanDurationNote")} {StateManager.StringsManager.GetString("TimeConsumptionNote").Pastel(MelonColor.Highlight)} {StateManager.StringsManager.GetString("FileCountNote")}".Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("StartConfirmation").Pastel(MelonColor.Text));
            var input = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
            if (input == PositiveConfirmation)
            {
                if (!Scanning)
                {
                    Thread scanThread = new Thread(StartScan);
                    scanThread.Start(true);
                    DisplayManager.UIExtensions.Add("LibraryScanIndicator", () => 
                    { 
                        Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight));
                    });
                    MelonUI.endOptionsDisplay = true;
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
                    double scanned = ScannedFiles;
                    double found = FoundFiles;
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
                        var msg = $"{StateManager.StringsManager.GetString("ScanStatus")} {scanned.ToString().Pastel(MelonColor.Melon)} // {found.ToString().Pastel(MelonColor.Melon)} {StateManager.StringsManager.GetString("FoundStatus")}";
                        Console.Write(msg);
                        Console.WriteLine(new string(' ', Console.WindowWidth - msg.Length - 1));
                        MelonUI.DisplayProgressBar(scanned, found, '#', '-');
                        //Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"{StateManager.StringsManager.GetString("SystemStatusDisplay")}: {CurrentStatus}  ";
                        var max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.Write(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.WriteLine(new string(' ', Console.WindowWidth - msg.Length - 1));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                    }
                    catch (Exception)
                    {

                    }
                }
            });
            DisplayThread.Priority = ThreadPriority.Highest;
            DisplayThread.Start();


            while (!endDisplay)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey();
                    if (k.Key == ConsoleKey.Escape)
                    {
                        endDisplay = true;
                        return;
                    }
                }
            }

        }
    }
}
