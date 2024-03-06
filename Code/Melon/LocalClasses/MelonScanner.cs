using Amazon.Util.Internal;
using ATL.Logging;
using Melon.Classes;
using Melon.DisplayClasses;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        public static bool Indexed { get; set; }
        public static bool endDisplay { get; set; }
        public static bool Scanning { get; set; }
        private static List<string> LyricFiles { get; set; }
        private static Stopwatch watch { get; set; }
        private static IMongoDatabase NewMelonDB { get; set; }
        private static IMongoCollection<Artist> ArtistCollection { get; set; }
        private static IMongoCollection<Album> AlbumCollection { get; set; }
        private static IMongoCollection<Track> TracksCollection { get; set; }

        // Main Scanning Functions
        public static void StartScan(object skipBool)
        {
            if (Scanning)
            {
                return;
            }

            Scanning = true;

            // Get MongoDB collections
            NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");


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

            foreach(var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolder(path, skip);
            }

            CurrentFolder = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("TrackSortingStatus");
            Sort();

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
        private static void DeletePass()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");

            int page = 0;
            int count = 100;

            ScannedFiles = 0;
            FoundFiles = TracksCollection.Count(Builders<Track>.Filter.Empty);
            while (true)
            {
                var trackFilter = Builders<Track>.Filter.Regex(x => x.Name, new BsonRegularExpression("", "i"));
                var tracks = TracksCollection.Find(trackFilter)
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

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
                    ScannedFiles++;
                }

                if(tracks.Count() != 100)
                {
                    break;
                }
                page++;
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
                watch.Restart();

                int count = 0;
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
                        TryMatchLRC(file);

                        ScannedFiles++;
                        continue;
                    }

                    // Move on if file is not an Audio file
                    if (!IsAudioFile(file))
                    {
                        ScannedFiles++;
                        continue;
                    }

                    // Get the file metadata
                    var fileMetadata = new ATL.Track(file);

                    // Attempt to find the track if it's already in the DB
                    Track trackDoc = null;
                    var trackfilter = Builders<Track>.Filter.Eq(x=>x.Path, file);
                    trackDoc = TracksCollection.Find(trackfilter).FirstOrDefault();

                    // If the scanner is set to fast mode, check file to see when it was last changed.
                    // Skip if the track hasn't changed since the last scan.
                    if (skip)
                    {
                        if (trackDoc != null)
                        {
                            DateTime lastModified = System.IO.File.GetLastWriteTime(file).ToUniversalTime();
                            if(trackDoc.LastModified.ToString("MM/dd/yyyy hh:mm:ss") == lastModified.ToString("MM/dd/yyyy hh:mm:ss"))
                            {
                                watch.Stop();
                                Task timeTask = new Task(() =>
                                {
                                    averageMilliseconds += watch.ElapsedMilliseconds;
                                    ScannedFiles++;
                                });
                                timeTask.Start();
                                
                                continue;
                            }
                        }
                    }

                    Task s = new Task(() =>
                    {
                        CurrentStatus = StateManager.StringsManager.GetString("TagPreparationStatus");
                    });
                    s.Start();

                    // Get and Split the artists metadata tag
                    List<string> albumArtists = SplitArtists(fileMetadata.AlbumArtist);
                    List<string> trackArtists = SplitArtists(fileMetadata.Artist);

                    // Split Genres
                    List<string> trackGenres = SplitGenres(fileMetadata.Genre);

                    // Conform Genres
                    // https://github.com/EpsiRho/MelonMediaServer/issues/12

                    // Generate IDs
                    List<string> TrackArtistIds = new List<string>();
                    List<string> AlbumArtistIds = new List<string>();
                    GenerateArtistIDs(trackArtists, albumArtists, out AlbumArtistIds, out TrackArtistIds);

                    // Attempt to find the album if it's already in the DB
                    var albumFilter = Builders<Album>.Filter.Eq(x=>x.Name, fileMetadata.Album);
                    albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.Name", albumArtists[0]);
                    var albumDoc = AlbumCollection.Find(albumFilter).FirstOrDefault();

                    // Generate / Get IDs for the track and album
                    string AlbumId = ObjectId.GenerateNewId().ToString();
                    if (albumDoc != null)
                    {
                        AlbumId = albumDoc._id;
                    }
                    string TrackId = ObjectId.GenerateNewId().ToString();
                    if (trackDoc != null)
                    {
                        TrackId = trackDoc._id;
                    }

                    // Create new ShortAlbum and ShortTrack
                    DbLink sAlbum = CreateShortAlbum(AlbumId, fileMetadata.Album);
                    DbLink sTrack = CreateShortTrack(fileMetadata, TrackId);

                    // Combine artists lists, then for each artists add / update data.
                    var combinedArtists = new List<string>();
                    combinedArtists.AddRange(trackArtists);
                    combinedArtists.AddRange(albumArtists);
                    foreach (var artistName in combinedArtists)
                    {

                        // Get the ID needed
                        string aId = "";
                        if (count >= TrackArtistIds.Count())
                        {
                            aId = AlbumArtistIds[count - TrackArtistIds.Count()];
                        }
                        else
                        {
                            aId = TrackArtistIds[count];
                        }

                        // Create or Update the artist
                        (Artist artistDoc, bool created) = CreateOrUpdateArtist(fileMetadata, artistName, aId, 
                                                                                sAlbum, sTrack, trackDoc, albumArtists, trackArtists,
                                                                                AlbumArtistIds, TrackArtistIds, trackGenres);

                        // If the artist was found and updated, make sure the list of IDs gets fixed
                        if (!created)
                        {
                            if (count >= TrackArtistIds.Count())
                            {
                                AlbumArtistIds[count - TrackArtistIds.Count()] = artistDoc._id;
                            }
                            else
                            {
                                TrackArtistIds[count] = artistDoc._id;
                            }
                        }

                        CreateOrUpdateAlbum(ref albumDoc, trackDoc, AlbumId, fileMetadata, albumArtists,
                                            AlbumArtistIds, trackArtists, TrackArtistIds, trackGenres, sTrack);

                        CreateOrUpdateTrack(TrackId, fileMetadata, sAlbum, albumArtists, trackArtists,
                                            AlbumArtistIds, TrackArtistIds, count, trackGenres, trackDoc);

                        count++;
                    }
                }
                catch (Exception e)
                {
                    if(e.Message.Contains("DuplicateKey"))
                    {
                        ScannedFiles++;
                        count++;
                        continue;
                    }

                    var FailedCollection = NewMelonDB.GetCollection<FailedFile>("FailedFiles");

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
                ScannedFiles++;
            };
            
        }

        // Scanning Section Functions
        private static void TryMatchLRC(string file)
        {
            // Try to find a track with the same filename as the lrc file (- the extension)
            var filename = Path.GetFileName(file);
            Track t = null;
            var tf = Builders<Track>.Filter.Where(x => x.Path.StartsWith(file.Replace(".lrc", "")));
            t = TracksCollection.Find(tf).FirstOrDefault();

            if (t != null) // If found, add to the track
            {
                t.LyricsPath = file;
                TracksCollection.ReplaceOne(tf, t);
            }
            else // If not found, the track it belongs to may show up later. Add to list to check later.
            {
                MelonScanner.LyricFiles.Add(file);
            }
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
        private static void GenerateArtistIDs(List<string> trackArtists, List<string> albumArtists, out List<string> AlbumArtistIds, out List<string> TrackArtistIds)
        {
            TrackArtistIds = new List<string>();
            AlbumArtistIds = new List<string>();

            // For each found track artist, try to find the artist in the db
            for (int i = 0; i < trackArtists.Count(); i++)
            {
                var artistFilter = Builders<Artist>.Filter.Eq(x => x.Name, trackArtists[i]);
                var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();

                if (artistDoc == null) // If the track doesn't exist, create a new ID
                {
                    TrackArtistIds.Add(ObjectId.GenerateNewId().ToString());
                }
                else // If the track does exist, inherit the existing ID
                {
                    TrackArtistIds.Add(artistDoc._id);
                }
            }

            // For each found album artist, try to find the artist in the db
            for (int i = 0; i < albumArtists.Count(); i++)
            {
                var artistFilter = Builders<Artist>.Filter.Eq(x => x.Name, albumArtists[i]);
                var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();

                if (artistDoc == null) // If the track doesn't exist, create a new ID
                {
                    AlbumArtistIds.Add(ObjectId.GenerateNewId().ToString());
                }
                else // If the track does exist, inherit the existing ID
                {
                    AlbumArtistIds.Add(artistDoc._id);
                }
            }
        }
        private static DbLink CreateShortAlbum(string AlbumId, string name)
        {
            if(name == "")
            {
                name = "Unknown Album";
            }
            // Create the album
            DbLink sAlbum = new DbLink()
            {
                _id = AlbumId,
                Name = name
            };

            return sAlbum;
        }

        private static DbLink CreateShortTrack(ATL.Track fileMetadata, string TrackId)
        {
            DbLink sTrack = new DbLink()
            {
                _id = TrackId,
                Name = fileMetadata.Title
            };

            return sTrack;
        }

        private static (Artist, bool) CreateOrUpdateArtist(ATL.Track fileMetadata, string artistName, string aId, DbLink sAlbum,
                                                          DbLink sTrack, Track trackDoc, List<string> albumArtists, List<string> trackArtists, 
                                                          List<string> AlbumArtistIds, List<string> TrackArtistIds, List<string> trackGenres)
        {
            // If artist name is nothing, set to "Unknown Artist"
            string artist = string.IsNullOrEmpty(artistName) ? StateManager.StringsManager.GetString("UnknownArtistStatus") : artistName;

            // Try to find the artist
            var artistFilter = Builders<Artist>.Filter.Eq(x => x.Name, artist);
            var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();

            // If the artist was not found, create a new one
            if (artistDoc == null)
            {
                // Create Artist Object
                Task s = new Task(() =>
                {
                    CurrentStatus = $"{StateManager.StringsManager.GetString("AdditionProcess")} {artist}";
                });
                s.Start();
                artistDoc = new Artist()
                {
                    _id = aId,
                    Name = artist,
                    Bio = "",
                    Ratings = new List<UserStat>(),
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Releases = new List<DbLink>(),
                    Genres = new List<string>(),
                    SeenOn = new List<DbLink>(),
                    Tracks = new List<DbLink> { sTrack },
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
                };

                if (trackArtists.Count() > 1)
                {
                    for (int i = 0; i < trackArtists.Count(); i++)
                    {
                        if (aId != TrackArtistIds[i])
                        {
                            artistDoc.ConnectedArtists.Add(new DbLink() { Name = trackArtists[i], _id = TrackArtistIds[i] });
                        }
                    }

                }

                if (albumArtists.Count() > 1)
                {
                    for (int i = 0; i < albumArtists.Count(); i++)
                    {
                        if (aId != AlbumArtistIds[i])
                        {
                            artistDoc.ConnectedArtists.Add(new DbLink() { Name = albumArtists[i], _id = AlbumArtistIds[i] });
                        }
                    }

                }

                // Add the release
                if (albumArtists.Contains(artist))
                {
                    artistDoc.Releases.Add(sAlbum);
                }
                else
                {
                    artistDoc.SeenOn.Add(sAlbum);
                }

                // Add the artist to the db
                ArtistCollection.InsertOne(artistDoc);
                return (artistDoc, true);
            }
            else // If the artist was found, update it
            {
                Task s = new Task(() =>
                {
                    CurrentStatus = $"{StateManager.StringsManager.GetString("UpdateProcess")} {artist}";
                });
                s.Start();

                if (trackArtists.Count() > 1)
                {
                    for (int i = 0; i < trackArtists.Count(); i++)
                    {
                        if (aId.ToString() != TrackArtistIds[i].ToString() && artistDoc.ConnectedArtists.Where(x=>x._id == TrackArtistIds[i].ToString()).FirstOrDefault() == null)
                        {
                            artistDoc.ConnectedArtists.Add(new DbLink() { Name = trackArtists[i], _id = TrackArtistIds[i] });
                        }
                    }

                }

                if (albumArtists.Count() > 1)
                {
                    for (int i = 0; i < albumArtists.Count(); i++)
                    {
                        if (aId.ToString() != AlbumArtistIds[i].ToString() && artistDoc.ConnectedArtists.Where(x => x._id == AlbumArtistIds[i].ToString()).FirstOrDefault() == null)
                        {
                            artistDoc.ConnectedArtists.Add(new DbLink() { Name = albumArtists[i], _id = AlbumArtistIds[i] });
                        }
                    }

                }

                if (albumArtists.Contains(artist)) // If it's an album artists, it's their release
                {
                    if (!artistDoc.Releases.Any(release => release.Name == fileMetadata.Album))
                    {
                        if (trackDoc != null)
                        {
                            artistDoc.Releases.Remove(trackDoc.Album);
                        }
                        artistDoc.Releases.Add(sAlbum);
                    }
                }
                else // This release is not from the artist
                {
                    // Check if it's in the db already
                    if (!artistDoc.SeenOn.Any(release => release.Name == fileMetadata.Album))
                    {
                        if (trackDoc != null)
                        {
                            artistDoc.SeenOn.Remove(trackDoc.Album);
                        }
                        artistDoc.SeenOn.Add(sAlbum);
                    }
                }

                // Check if the artist contains the track
                if (trackDoc != null)
                {
                    artistDoc.Tracks.RemoveAll(t => t._id == trackDoc._id);
                }

                artistDoc.Tracks.Add(sTrack); // Replace it

                // Att the track genres to the artist's genres if they don't already have it
                foreach (var genre in trackGenres)
                {
                    if (!artistDoc.Genres.Contains(genre))
                    {
                        artistDoc.Genres.Add(genre);
                    }
                }

                // Update the artist
                
                ArtistCollection.ReplaceOne(artistFilter, artistDoc);
                return (artistDoc, false);
            }
        }
        private static void CreateOrUpdateAlbum(ref Album albumDoc, Track trackDoc, string AlbumId, ATL.Track fileMetadata, List<string> albumArtists,
                                               List<string> AlbumArtistIds, List<string> trackArtists, List<string> TrackArtistIds,
                                               List<string> trackGenres, DbLink sTrack)
        {
            var albumFilter = Builders<Album>.Filter.Eq(x => x.Name, fileMetadata.Album);
            albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.Name", albumArtists[0]);

            if (albumDoc == null) // If albumDoc is null, make a new album.
            {
                // Set the album name to "Unkown album" if no album name is found.
                string albumName = string.IsNullOrEmpty(fileMetadata.Album) ? StateManager.StringsManager.GetString("UnknownAlbumStatus") : fileMetadata.Album;

                Task s = new Task(() =>
                {
                    CurrentStatus = $"{StateManager.StringsManager.GetString("AdditionProcess")} {fileMetadata.Album}";
                });
                s.Start();

                // Create the new album object
                albumDoc = new Album
                {
                    _id = AlbumId,
                    Name = albumName,
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Bio = "",
                    TotalDiscs = fileMetadata.DiscTotal ?? 1,
                    TotalTracks = fileMetadata.TrackTotal ?? 0,
                    Publisher = fileMetadata.Publisher ?? "",
                    ReleaseStatus = fileMetadata.AdditionalFields.TryGetValue("RELEASESTATUS", out var rs) ? rs : "",
                    ReleaseType = fileMetadata.AdditionalFields.TryGetValue("RELEASETYPE", out var rt) ? rt : "",
                    ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                    AlbumArtists = new List<DbLink>(),
                    AlbumArtPaths = new List<string>(),
                    Tracks = new List<DbLink>(),
                    ContributingArtists = new List<DbLink>(),
                    AlbumGenres = trackGenres ?? new List<string>(),
                    AlbumArtCount = 0,
                    AlbumArtDefault = 0,
                    PlayCounts = new List<UserStat>(),
                    Ratings = new List<UserStat>(),
                    SkipCounts = new List<UserStat>(),
                    ServerURL = ""
                };

                // Get any images from the album and save them to the disk under the album ID
                // We only get the first track's images for each album
                // In theory, all tracks in an album will have their album art in every track file
                for (int i = 0; i < fileMetadata.EmbeddedPictures.Count(); i++)
                {
                    using (FileStream artFile = new FileStream($"{StateManager.melonPath}/AlbumArts/{albumDoc._id}-{i}.jpg", FileMode.Create, System.IO.FileAccess.Write))
                    {

                        byte[] bytes = fileMetadata.EmbeddedPictures[i].PictureData;
                        artFile.Write(bytes, 0, bytes.Length);
                    }
                    albumDoc.AlbumArtPaths.Add($"{albumDoc._id}-{i}.jpg");
                    albumDoc.AlbumArtCount++;
                }

                // Set the album's artists.
                for (int i = 0; i < albumArtists.Count(); i++)
                {
                    try
                    {
                        albumDoc.AlbumArtists.Add(new DbLink() { Name = albumArtists[i], _id = AlbumArtistIds[i] });
                    }
                    catch (Exception e)
                    {

                    }
                }

                // Set the album's contributing artists
                for (int i = 0; i < trackArtists.Count(); i++)
                {
                    try
                    {
                        albumDoc.ContributingArtists.Add(new DbLink() { Name = trackArtists[i], _id = TrackArtistIds[i] });
                    }
                    catch (Exception e)
                    {

                    }
                }

                // Add the track
                albumDoc.Tracks.Add(sTrack);

                // Add the new album to the db
                AlbumCollection.InsertOne(albumDoc);
            }
            else
            {
                Task s = new Task(() =>
                {
                    CurrentStatus = $"{StateManager.StringsManager.GetString("UpdateProcess")} {fileMetadata.Album}";
                });
                s.Start();
                // Add artists to album artists if they are not already listed
                for (int i = 0; i < albumArtists.Count(); i++)
                {
                    try
                    {
                        if (!albumDoc.AlbumArtists.Any(artist => artist._id == AlbumArtistIds[i].ToString()))
                        {
                            albumDoc.AlbumArtists.Add(new DbLink() { Name = albumArtists[i], _id = AlbumArtistIds[i] });
                        }
                    }
                    catch (Exception e)
                    {

                    }
                }

                // Add track artists to contributing artists
                for (int i = 0; i < trackArtists.Count(); i++)
                {
                    try
                    {
                        if (!albumDoc.ContributingArtists.Any(artist => artist._id == TrackArtistIds[i].ToString()))
                        {
                            albumDoc.ContributingArtists.Add(new DbLink() { Name = trackArtists[i], _id = TrackArtistIds[i] });
                        }
                    }
                    catch (Exception e)
                    {

                    }
                }

                // Add track genres to the album
                foreach (var genre in trackGenres)
                {
                    if (!albumDoc.AlbumGenres.Contains(genre))
                    {
                        albumDoc.AlbumGenres.Add(genre);
                    }
                }

                // Add the current track if not already listed
                var existingTrack = albumDoc.Tracks.FirstOrDefault(t => t._id == sTrack._id);
                if (existingTrack != null) // if listed, remove the old one
                {
                    albumDoc.Tracks.Remove(existingTrack);
                }

                // Then re-add it. This is to make sure track gets updated if file info changes.
                albumDoc.Tracks.Add(sTrack);

                // Update the album
                AlbumCollection.ReplaceOne(albumFilter, albumDoc);

            }
        }
        private static void CreateOrUpdateTrack(string TrackId, ATL.Track fileMetadata, DbLink sAlbum, List<string> albumArtists,
                                               List<string> trackArtists, List<string> AlbumArtistIds, List<string> TrackArtistIds, int count,
                                               List<string> trackGenres, Track trackDoc)
        {
            if(fileMetadata.Title == "08 Porter Robinson - Unison (Mikkas remix)")
            {
                Debug.WriteLine("Here");
            }
            // Create the new track object
            Track track = new Track
            {
                _id = TrackId,
                LastModified = File.GetLastWriteTime(fileMetadata.Path).ToUniversalTime(),
                DateAdded = DateTime.UtcNow,
                Name = fileMetadata.Title ?? StateManager.StringsManager.GetString("UnknownStatus"),
                Album = sAlbum,
                Path = fileMetadata.Path.Replace("\\", "/"),
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
                TrackArtists = new List<DbLink>(),
                TrackGenres = trackGenres ?? new List<string>(),
                ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                LyricsPath = "",
                nextTrack = "",
                ServerURL = "",
            };

            Task s = new Task(() =>
            {
                CurrentStatus = $"{StateManager.StringsManager.GetString("AdditionProcess")} / {StateManager.StringsManager.GetString("UpdateProcess")} {track.Name}";
            });
            s.Start();

            // Check if any of the found lyric files match with the new track
            for (int i = 0; i < MelonScanner.LyricFiles.Count(); i++)
            {
                if (track.Path.StartsWith(MelonScanner.LyricFiles[i].Replace(".lrc", "")))
                {
                    track.LyricsPath = MelonScanner.LyricFiles[i];
                    MelonScanner.LyricFiles.Remove(MelonScanner.LyricFiles[i]);
                    break;
                }
            }

            // Add track artists
            for (int i = 0; i < trackArtists.Count(); i++)
            {
                if (count >= TrackArtistIds.Count())
                {
                    if(track.TrackArtists.Where(x=>x._id == AlbumArtistIds[count - TrackArtistIds.Count()]).Count() == 0)
                    {
                        track.TrackArtists.Add(new DbLink() { _id = AlbumArtistIds[count - TrackArtistIds.Count()], Name = albumArtists[count - TrackArtistIds.Count()] });
                    }
                }
                else
                {
                    if (track.TrackArtists.Where(x => x._id == TrackArtistIds[i]).Count() == 0)
                    {
                        track.TrackArtists.Add(new DbLink() { _id = TrackArtistIds[i], Name = trackArtists[i] });
                    }
                }
            }

            // If the trackDoc is null, the track doesn't exist. Add the new track
            if (trackDoc == null)
            {
                try
                {
                    TracksCollection.InsertOne(track);
                }
                catch (Exception e)
                {
                    return;
                }
            }
            else // Otherwise, use the new track information to replace the old one
            {
                track._id = trackDoc._id;
                track._id = trackDoc._id.ToString();
                var trackfilter = Builders<Track>.Filter.Eq("Path", fileMetadata.Path);
                TracksCollection.ReplaceOne(trackfilter, track);

                // remove old tracks from old albums
                if (trackDoc.Album._id != track.Album._id)
                {
                    var aFilter = Builders<Album>.Filter.Eq(x => x._id, trackDoc.Album._id);
                    var album = AlbumCollection.Find(aFilter).ToList();
                    if (album.Count() != 0)
                    {
                        album[0].Tracks.Remove(new DbLink(trackDoc));
                    }
                }

                // remove old tracks from old artists
                foreach (var a in trackDoc.TrackArtists)
                {
                    if (!track.TrackArtists.Contains(a))
                    {
                        var aFilter = Builders<Artist>.Filter.Eq(x => x._id, a._id);
                        var aritst = ArtistCollection.Find(aFilter).ToList();
                        if (aritst.Count() != 0)
                        {
                            aritst[0].Tracks.Remove(new DbLink(trackDoc));
                        }
                    }
                }
            }
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
                    var filter = Builders<Track>.Filter.In(a => a._id, albumDoc.Tracks.Select(x=>x._id));
                    var fullTracks = TracksCollection.Find(filter).ToList();
                    albumDoc.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new DbLink() { _id = x._id, Name = x.Name }).ToList();
                    albumDoc.TotalTracks = albumDoc.Tracks.Count();
                    var albumFilter = Builders<Album>.Filter.Eq(x=>x._id, albumDoc._id);
                    AlbumCollection.ReplaceOneAsync(albumFilter, albumDoc);
                    ScannedFiles++;
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
                ArtistCollection.Indexes.CreateOne(artistIndexModel);

                var albumIndexKeysDefinition = Builders<Album>.IndexKeys.Ascending(x => x.Name);
                var albumIndexModel = new CreateIndexModel<Album>(albumIndexKeysDefinition, indexOptions);
                AlbumCollection.Indexes.CreateOne(albumIndexModel);

                var trackIndexKeysDefinition = Builders<Track>.IndexKeys.Ascending(x => x.Name);
                var trackIndexModel = new CreateIndexModel<Track>(trackIndexKeysDefinition, indexOptions);
                TracksCollection.Indexes.CreateOne(trackIndexModel);
            }
            catch (Exception)
            {

            }
        }
        public static void ResetDb()
        {
            MelonUI.ShowIndeterminateProgress();
            NewMelonDB = StateManager.DbClient.GetDatabase("Melon");

            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            TracksCollection.DeleteMany(Builders<Track>.Filter.Empty);
            TracksCollection.Indexes.DropAll();

            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            ArtistCollection.DeleteMany(Builders<Artist>.Filter.Empty);
            ArtistCollection.Indexes.DropAll();

            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            AlbumCollection.DeleteMany(Builders<Album>.Filter.Empty);
            AlbumCollection.Indexes.DropAll();

            var QueueCollection = NewMelonDB.GetCollection<PlayQueue>("Queues");
            QueueCollection.DeleteMany(Builders<PlayQueue>.Filter.Empty);

            var PlaylistCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
            PlaylistCollection.DeleteMany(Builders<Playlist>.Filter.Empty);

            var collectionCollection = NewMelonDB.GetCollection<Collection>("Collections");
            collectionCollection.DeleteMany(Builders<Collection>.Filter.Empty);

            var failedCollection = NewMelonDB.GetCollection<FailedFile>("FailedFiles");
            failedCollection.DeleteMany(Builders<FailedFile>.Filter.Empty);

            var metadataCollection = NewMelonDB.GetCollection<DbMetadata>("Metadata");
            metadataCollection.DeleteMany(Builders<DbMetadata>.Filter.Empty);

            var statsCollection = NewMelonDB.GetCollection<PlayStat>("Stats");
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
                    Thread scanThread = new Thread(MelonScanner.StartScan);
                    scanThread.Start(false);
                    DisplayManager.UIExtensions.Add("LibraryScanIndicator",() => { Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.Remove("LibraryScanIndicator"); });
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
                    Thread scanThread = new Thread(MelonScanner.StartScan);
                    scanThread.Start(true);
                    DisplayManager.UIExtensions.Add("LibraryScanIndicator", () => { Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.Remove("LibraryScanIndicator"); });
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
