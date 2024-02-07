using Melon.Classes;
using Melon.DisplayClasses;
using Melon.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System.Collections.Generic;
using System.Diagnostics;

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
        private static List<string> FoundPaths { get; set; }
        public static List<string> LyricFiles { get; set; }
        private static Stopwatch watch { get; set; }

        // Main Scanning Functions
        public static void StartScan(object skipBool)
        {
            if (Scanning)
            {
                return;
            }

            Scanning = true;

            bool skip = (bool)skipBool;
            LyricFiles = new List<string>();
            watch = new Stopwatch();
            ScannedFiles = 0;
            FoundFiles = 0;
            averageMilliseconds = 0;
            Indexed = false;
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
            endDisplay = true;
            CurrentFile = StateManager.StringsManager.GetString("NotApplicableStatus");
            CurrentStatus = StateManager.StringsManager.GetString("CompletionStatus");
            IndexCollections();
            DisplayManager.UIExtensions.Clear();
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
                var trackFilter = Builders<Track>.Filter.Regex("TrackName", new BsonRegularExpression("", "i"));
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
            CurrentFolder = path;
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolder(folder, skip);
            }

            // Get MongoDB collections
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");

            // Get Files, for each file:
            var files = Directory.GetFiles(path);
            foreach(var file in files)
            {
                watch.Restart();

                int count = 0;
                try
                {
                    CurrentFile = file;
                    var filename = Path.GetFileName(file);

                    // Lyrics matcher
                    if (filename.EndsWith(".lrc")) 
                    {
                        TryMatchLRC(file, TracksCollection);

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
                    var trackfilter = Builders<Track>.Filter.Eq("Path", file);
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
                                averageMilliseconds += watch.ElapsedMilliseconds;
                                ScannedFiles++;
                                continue;
                            }
                        }
                    }

                    CurrentStatus = StateManager.StringsManager.GetString("TagPreparationStatus");

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
                    GenerateArtistIDs(trackArtists, albumArtists, ArtistCollection, out AlbumArtistIds, out TrackArtistIds);

                    // Attempt to find the album if it's already in the DB
                    var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Album);
                    albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumArtists[0]);
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
                    ShortAlbum sAlbum = CreateShortAlbum(fileMetadata, AlbumId, albumDoc, trackArtists, 
                                                                           albumArtists, TrackArtistIds, AlbumArtistIds, fileMetadata.Album);
                    ShortTrack sTrack = CreateShortTrack(fileMetadata, TrackId, sAlbum, trackArtists, TrackArtistIds);

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
                        (Artist artistDoc, bool created) = CreateOrUpdateArtist(ArtistCollection, fileMetadata, artistName, aId, 
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
                                                              AlbumArtistIds, trackArtists, TrackArtistIds, trackGenres, sTrack,
                                                              AlbumCollection);

                        CreateOrUpdateTrack(TrackId, fileMetadata, sAlbum, albumArtists, trackArtists,
                                                               AlbumArtistIds, TrackArtistIds, count, trackGenres, trackDoc,
                                                               TracksCollection, AlbumCollection, ArtistCollection);

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
        public static void TryMatchLRC(string file, IMongoCollection<Track> TracksCollection)
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

        public static bool IsAudioFile(string path)
        {
            var filename = Path.GetFileName(path);
            if (!filename.EndsWith(".flac") && !filename.EndsWith(".aac") && !filename.EndsWith(".wma") &&
                !filename.EndsWith(".wav") && !filename.EndsWith(".mp3") && !filename.EndsWith(".m4a"))
            {
                return false;
            }

            return true;
        }
        public static List<string> SplitArtists(string artistsStr)
        {
            // TODO: Allow changing the list of delimiters
            List<string> artists = new List<string>();
            var aSplit = artistsStr.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries);
            foreach (var a in aSplit)
            {
                if (!artists.Contains(a))
                {
                    artists.Add(a);
                }
            }

            return artists;
        }
        public static List<string> SplitGenres(string genresStr)
        {
            // TODO: Allow changing the list of delimiters
            List<string> genres = new List<string>();
            var gSplit = genresStr.Split(new string[] { ",", ";", "/" }, StringSplitOptions.TrimEntries);
            foreach (var g in gSplit)
            {
                if (!genres.Contains(g))
                {
                    genres.Add(g);
                }
            }

            genres.Remove("");

            return genres;
        }
        public static void GenerateArtistIDs(List<string> trackArtists, List<string> albumArtists, IMongoCollection<Artist> ArtistsCollection,
                                             out List<string> AlbumArtistIds, out List<string> TrackArtistIds)
        {
            TrackArtistIds = new List<string>();
            AlbumArtistIds = new List<string>();

            // For each found track artist, try to find the artist in the db
            for (int i = 0; i < trackArtists.Count(); i++)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", trackArtists[i]);
                var artistDoc = ArtistsCollection.Find(artistFilter).FirstOrDefault();

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
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", albumArtists[i]);
                var artistDoc = ArtistsCollection.Find(artistFilter).FirstOrDefault();

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
        public static ShortAlbum CreateShortAlbum(ATL.Track fileMetadata, string AlbumId, Album albumDoc, List<string> trackArtists, List<string> albumArtists,
                                                  List<string> TrackArtistIds, List<string> AlbumArtistIds, string name)
        {
            // Create the album
            ShortAlbum sAlbum = new ShortAlbum()
            {
                _id = AlbumId,
                AlbumName = name
            };

            return sAlbum;
        }

        public static ShortTrack CreateShortTrack(ATL.Track fileMetadata, string TrackId, ShortAlbum sAlbum,
                                                  List<string> trackArtists, List<string> TrackArtistIds)
        {
            ShortTrack sTrack = new ShortTrack()
            {
                _id = TrackId,
                TrackName = fileMetadata.Title
            };

            return sTrack;
        }

        public static (Artist, bool) CreateOrUpdateArtist(IMongoCollection<Artist> ArtistCollection, ATL.Track fileMetadata, string artistName, string aId, ShortAlbum sAlbum,
                                                          ShortTrack sTrack, Track trackDoc, List<string> albumArtists, List<string> trackArtists, List<string> AlbumArtistIds, List<string> TrackArtistIds, List<string> trackGenres)
        {
            // If artist name is nothing, set to "Unknown Artist"
            string artist = string.IsNullOrEmpty(artistName) ? StateManager.StringsManager.GetString("UnknownArtistStatus") : artistName;

            // Try to find the artist
            var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", artist);
            var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();

            // If the artist was not found, create a new one
            if (artistDoc == null)
            {
                // Create Artist Object
                MelonScanner.CurrentStatus = $"{StateManager.StringsManager.GetString("AdditionProcess")} {artist}";
                artistDoc = new Artist()
                {
                    _id = aId,
                    ArtistName = artist,
                    Bio = "",
                    Ratings = new List<UserStat>(),
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Releases = new List<ShortAlbum>(),
                    Genres = new List<string>(),
                    SeenOn = new List<ShortAlbum>(),
                    Tracks = new List<ShortTrack> { sTrack },
                    ConnectedArtists = new List<ShortArtist>()
                };

                if (trackArtists.Count() > 1)
                {
                    for (int i = 0; i < trackArtists.Count(); i++)
                    {
                        if (aId != TrackArtistIds[i])
                        {
                            artistDoc.ConnectedArtists.Add(new ShortArtist() { ArtistName = trackArtists[i], _id = TrackArtistIds[i] });
                        }
                    }

                }

                if (albumArtists.Count() > 1)
                {
                    for (int i = 0; i < albumArtists.Count(); i++)
                    {
                        if (aId != AlbumArtistIds[i])
                        {
                            artistDoc.ConnectedArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], _id = AlbumArtistIds[i] });
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
                MelonScanner.CurrentStatus = $"{StateManager.StringsManager.GetString("UpdateProcess")} {artist}";

                if (trackArtists.Count() > 1)
                {
                    for (int i = 0; i < trackArtists.Count(); i++)
                    {
                        if (aId.ToString() != TrackArtistIds[i].ToString() && artistDoc.ConnectedArtists.Where(x=>x._id == TrackArtistIds[i].ToString()).FirstOrDefault() == null)
                        {
                            artistDoc.ConnectedArtists.Add(new ShortArtist() { ArtistName = trackArtists[i], _id = TrackArtistIds[i] });
                        }
                    }

                }

                if (albumArtists.Count() > 1)
                {
                    for (int i = 0; i < albumArtists.Count(); i++)
                    {
                        if (aId.ToString() != AlbumArtistIds[i].ToString() && artistDoc.ConnectedArtists.Where(x => x._id == AlbumArtistIds[i].ToString()).FirstOrDefault() == null)
                        {
                            artistDoc.ConnectedArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], _id = AlbumArtistIds[i] });
                        }
                    }

                }

                if (albumArtists.Contains(artist)) // If it's an album artists, it's their release
                {
                    if (!artistDoc.Releases.Any(release => release.AlbumName == fileMetadata.Album))
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
                    if (!artistDoc.SeenOn.Any(release => release.AlbumName == fileMetadata.Album))
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
        public static void CreateOrUpdateAlbum(ref Album albumDoc, Track trackDoc, string AlbumId, ATL.Track fileMetadata, List<string> albumArtists,
                                               List<string> AlbumArtistIds, List<string> trackArtists, List<string> TrackArtistIds,
                                               List<string> trackGenres, ShortTrack sTrack, IMongoCollection<Album> AlbumCollection)
        {
            var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Album);
            albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumArtists[0]);

            if (albumDoc == null) // If albumDoc is null, make a new album.
            {
                // Set the album name to "Unkown album" if no album name is found.
                string albumName = string.IsNullOrEmpty(fileMetadata.Album) ? StateManager.StringsManager.GetString("UnknownAlbumStatus") : fileMetadata.Album;

                MelonScanner.CurrentStatus = $"{StateManager.StringsManager.GetString("AdditionProcess")} {fileMetadata.Album}";

                // Create the new album object
                albumDoc = new Album
                {
                    _id = AlbumId,
                    AlbumName = albumName,
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Bio = "",
                    TotalDiscs = fileMetadata.DiscTotal ?? 1,
                    TotalTracks = fileMetadata.TrackTotal ?? 0,
                    Publisher = fileMetadata.Publisher ?? "",
                    ReleaseStatus = fileMetadata.AdditionalFields.TryGetValue("RELEASESTATUS", out var rs) ? rs : "",
                    ReleaseType = fileMetadata.AdditionalFields.TryGetValue("RELEASETYPE", out var rt) ? rt : "",
                    ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                    AlbumArtists = new List<ShortArtist>(),
                    AlbumArtPaths = new List<string>(),
                    Tracks = new List<ShortTrack>(),
                    ContributingArtists = new List<ShortArtist>(),
                    AlbumGenres = trackGenres ?? new List<string>()
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
                }

                // Set the album's artists.
                for (int i = 0; i < albumArtists.Count(); i++)
                {
                    try
                    {
                        albumDoc.AlbumArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], _id = AlbumArtistIds[i] });
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
                        albumDoc.ContributingArtists.Add(new ShortArtist() { ArtistName = trackArtists[i], _id = TrackArtistIds[i] });
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
                MelonScanner.CurrentStatus = $"{StateManager.StringsManager.GetString("UpdateProcess")} {fileMetadata.Album}";
                // Add artists to album artists if they are not already listed
                for (int i = 0; i < albumArtists.Count(); i++)
                {
                    try
                    {
                        if (!albumDoc.AlbumArtists.Any(artist => artist._id == AlbumArtistIds[i].ToString()))
                        {
                            albumDoc.AlbumArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], _id = AlbumArtistIds[i] });
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
                            albumDoc.ContributingArtists.Add(new ShortArtist() { ArtistName = trackArtists[i], _id = TrackArtistIds[i] });
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
        public static void CreateOrUpdateTrack(string TrackId, ATL.Track fileMetadata, ShortAlbum sAlbum, List<string> albumArtists,
                                               List<string> trackArtists, List<string> AlbumArtistIds, List<string> TrackArtistIds, int count,
                                               List<string> trackGenres, Track trackDoc, IMongoCollection<Track> TracksCollection,
                                               IMongoCollection<Album> AlbumCollection, IMongoCollection<Artist> ArtistCollection)
        {
            // Create the new track object
            Track track = new Track
            {
                _id = TrackId,
                LastModified = File.GetLastWriteTime(fileMetadata.Path).ToUniversalTime(),
                DateAdded = DateTime.UtcNow,
                TrackName = fileMetadata.Title ?? StateManager.StringsManager.GetString("UnknownStatus"),
                Album = sAlbum,
                Path = fileMetadata.Path,
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
                Duration = fileMetadata.DurationMs.ToString() ?? "",
                TrackArtists = new List<ShortArtist>(),
                TrackGenres = trackGenres ?? new List<string>(),
                ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                LyricsPath = "",
                nextTrack = "",
                ServerURL = ""
            };

            MelonScanner.CurrentStatus = $"{StateManager.StringsManager.GetString("AdditionProcess")} / {StateManager.StringsManager.GetString("UpdateProcess")} {track.TrackName}";

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
                    track.TrackArtists.Add(new ShortArtist() { _id = AlbumArtistIds[count - TrackArtistIds.Count()], ArtistName = albumArtists[count - TrackArtistIds.Count()] });
                }
                else
                {
                    track.TrackArtists.Add(new ShortArtist() { _id = TrackArtistIds[i], ArtistName = trackArtists[i] });
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
                        album[0].Tracks.Remove(new ShortTrack(trackDoc));
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
                            aritst[0].Tracks.Remove(new ShortTrack(trackDoc));
                        }
                    }
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
                    albumDoc.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new ShortTrack() { _id = x._id, TrackName = x.TrackName }).ToList();
                    var albumFilter = Builders<Album>.Filter.Eq("AlbumName", albumDoc.AlbumName);
                    albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumDoc.AlbumArtists[0].ArtistName);
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
                    try { artistDoc.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x => new ShortTrack() { _id = x._id, TrackName = x.TrackName }).ToList(); } catch (Exception) { }
                    try { artistDoc.Releases = fullReleases.OrderBy(x => x.ReleaseDate).Select(x => new ShortAlbum() { _id = x._id, AlbumName = x.AlbumName }).ToList(); } catch (Exception) { }
                    try { artistDoc.SeenOn = fullSeenOn.OrderBy(x => x.ReleaseDate).Select(x => new ShortAlbum() { _id = x._id, AlbumName = x.AlbumName }).ToList(); } catch (Exception) { }
                    var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", artistDoc.ArtistName);
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
        public static void IndexCollections()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var indexOptions = new CreateIndexOptions { Background = true  }; 

            var trackIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("TrackName");
            var TracksCollection = NewMelonDB.GetCollection<BsonDocument>("Tracks");
            var trackIndexModel = new CreateIndexModel<BsonDocument>(trackIndexKeysDefinition, indexOptions);
            TracksCollection.Indexes.CreateOne(trackIndexModel);

            var artistIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("ArtistName");
            var ArtistCollection = NewMelonDB.GetCollection<BsonDocument>("Artists");
            var artistIndexModel = new CreateIndexModel<BsonDocument>(artistIndexKeysDefinition, indexOptions);
            ArtistCollection.Indexes.CreateOne(artistIndexModel);

            var albumIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("AlbumName");
            var AlbumCollection = NewMelonDB.GetCollection<BsonDocument>("Albums");
            var albumIndexModel = new CreateIndexModel<BsonDocument>(albumIndexKeysDefinition, indexOptions);
            AlbumCollection.Indexes.CreateOne(albumIndexModel);
        }

        // UI
        public static void ResetDB()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("DatabaseResetOption") });

            // Description
            Console.WriteLine(StateManager.StringsManager.GetString("DatabaseRemovalWarning").Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("RescanRequirement").Pastel(MelonColor.Text));
            Console.WriteLine(StateManager.StringsManager.GetString("ResetConfirmation").Pastel(MelonColor.Text));
            string PositiveConfirmation = StateManager.StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StateManager.StringsManager.GetString("NegativeConfirmation");
            var input = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
            if (input == PositiveConfirmation) 
            {

                var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");

                var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
                TracksCollection.DeleteMany(Builders<Track>.Filter.Empty);

                var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
                ArtistCollection.DeleteMany(Builders<Artist>.Filter.Empty);

                var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
                AlbumCollection.DeleteMany(Builders<Album>.Filter.Empty);

                var QueueCollection = NewMelonDB.GetCollection<PlayQueue>("Queues");
                QueueCollection.DeleteMany(Builders<PlayQueue>.Filter.Empty);

                var PlaylistCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
                PlaylistCollection.DeleteMany(Builders<Playlist>.Filter.Empty);

                var failedCollection = NewMelonDB.GetCollection<FailedFile>("FailedFiles");
                failedCollection.DeleteMany(Builders<FailedFile>.Filter.Empty);

                var statsCollection = NewMelonDB.GetCollection<PlayStat>("Stats");
                statsCollection.DeleteMany(Builders<PlayStat>.Filter.Empty);

                if (Directory.Exists($"{StateManager.melonPath}/AlbumArts/"))
                {
                    foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/AlbumArts/"))
                    {
                        File.Delete(file);
                    }
                }
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
                ScanProgressView();
                if (Scanning)
                {
                    Thread scanThread = new Thread(MelonScanner.StartScan);
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
