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
                Console.WriteLine("No library paths to search!".Pastel(MelonColor.Error));
                Console.WriteLine("Press any key to continue...".Pastel(MelonColor.BackgroundText));
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

            CurrentFolder = "N/A";
            CurrentFile = "N/A";
            CurrentStatus = "Sorting Tracks and Releases";
            Sort();

            CurrentFolder = "N/A";
            CurrentFile = "N/A";
            CurrentStatus = "Delete Pass, Finishing up~";
            DeletePass();
            endDisplay = true;
            CurrentFile = "N/A";
            CurrentStatus = "Complete!";
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
                        var filter = Builders<Album>.Filter.Eq(x=>x.AlbumId, track.Album.AlbumId);
                        var albums = AlbumCollection.Find(filter).ToList();

                        List<string> zeroed = new List<string>();
                        if(albums.Count() != 0)
                        {
                            var album = albums[0];
                            var query = (from t in album.Tracks
                                         where t.TrackId == track.TrackId
                                         select t).FirstOrDefault();
                            album.Tracks.Remove(query);
                            if (album.Tracks.Count == 0)
                            {
                                zeroed.Add(album.AlbumId);
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
                            var aFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, artist.ArtistId);
                            var artists = ArtistCollection.Find(aFilter).ToList();
                            Artist dbArtist = null;

                            if(artists.Count() != 0)
                            {
                                dbArtist = artists[0];
                                var query = (from t in dbArtist.Tracks
                                             where t.TrackId == track.TrackId
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
                                                where a.AlbumId == z
                                                select a).ToList();

                                        foreach(var a in q)
                                        {
                                            dbArtist.Releases.Remove(a);
                                        }

                                        q = (from a in dbArtist.SeenOn
                                             where a.AlbumId == z
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
                        var tFilter = Builders<Track>.Filter.Eq(x=>x.TrackId, track.TrackId);
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
            var FailedCollection = NewMelonDB.GetCollection<FailedFiles>("FailedFiles");

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

                    CurrentStatus = "Preparing Artist and Genre tags";

                    // Get and Split the artists metadata tag
                    List<string> albumArtists = SplitArtists(fileMetadata.AlbumArtist);
                    List<string> trackArtists = SplitArtists(fileMetadata.Artist);

                    // Split Genres
                    List<string> trackGenres = SplitGenres(fileMetadata.Genre);

                    // Conform Genres
                    // https://github.com/EpsiRho/MelonMediaServer/issues/12

                    // Generate IDs
                    List<MelonId> TrackArtistIds = new List<MelonId>();
                    List<MelonId> AlbumArtistIds = new List<MelonId>();
                    GenerateArtistIDs(trackArtists, albumArtists, ArtistCollection, out AlbumArtistIds, out TrackArtistIds);

                    // Attempt to find the album if it's already in the DB
                    var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Album);
                    albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumArtists[0]);
                    var albumDoc = AlbumCollection.Find(albumFilter).FirstOrDefault();

                    // Generate / Get IDs for the track and album
                    MelonId AlbumId = new MelonId(ObjectId.GenerateNewId());
                    if (albumDoc != null)
                    {
                        AlbumId = albumDoc._id;
                    }
                    MelonId TrackId = new MelonId(ObjectId.GenerateNewId());
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
                        MelonId aId = new MelonId();
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
                                                                                                  sAlbum, sTrack, trackDoc, albumArtists, trackGenres);

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


                    var fileFilter = Builders<FailedFiles>.Filter.Eq("Type", "Failed");
                    var fileDoc = FailedCollection.Find(fileFilter).FirstOrDefault();
                    if (fileDoc == null)
                    {
                        FailedFiles failed = new FailedFiles();
                        failed.Type = "Failed";
                        failed.Paths = new List<string>
                        {
                            file
                        };
                        FailedCollection.InsertOne(failed);

                    }
                    else
                    {
                        var arrayUpdateFailed = Builders<FailedFiles>.Update.Push("Paths", file);
                        FailedCollection.UpdateOne(fileFilter, arrayUpdateFailed);
                    }
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

            return genres;
        }
        public static void GenerateArtistIDs(List<string> trackArtists, List<string> albumArtists, IMongoCollection<Artist> ArtistsCollection,
                                             out List<MelonId> AlbumArtistIds, out List<MelonId> TrackArtistIds)
        {
            TrackArtistIds = new List<MelonId>();
            AlbumArtistIds = new List<MelonId>();

            // For each found track artist, try to find the artist in the db
            for (int i = 0; i < trackArtists.Count(); i++)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", trackArtists[i]);
                var artistDoc = ArtistsCollection.Find(artistFilter).FirstOrDefault();

                if (artistDoc == null) // If the track doesn't exist, create a new ID
                {
                    TrackArtistIds.Add(new MelonId(ObjectId.GenerateNewId()));
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
                    AlbumArtistIds.Add(new MelonId(ObjectId.GenerateNewId()));
                }
                else // If the track does exist, inherit the existing ID
                {
                    AlbumArtistIds.Add(artistDoc._id);
                }
            }
        }
        public static ShortAlbum CreateShortAlbum(ATL.Track fileMetadata, MelonId AlbumId, Album albumDoc, List<string> trackArtists, List<string> albumArtists,
                                                  List<MelonId> TrackArtistIds, List<MelonId> AlbumArtistIds, string name)
        {
            // Create the album
            ShortAlbum sAlbum = new ShortAlbum()
            {
                _id = AlbumId,
                AlbumId = AlbumId.ToString(),
                AlbumName = name
            };

            return sAlbum;
        }

        public static ShortTrack CreateShortTrack(ATL.Track fileMetadata, MelonId TrackId, ShortAlbum sAlbum,
                                                  List<string> trackArtists, List<MelonId> TrackArtistIds)
        {
            ShortTrack sTrack = new ShortTrack()
            {
                _id = TrackId,
                TrackId = TrackId.ToString(),
            };

            return sTrack;
        }

        public static (Artist, bool) CreateOrUpdateArtist(IMongoCollection<Artist> ArtistCollection, ATL.Track fileMetadata, string artistName, MelonId aId, ShortAlbum sAlbum,
                                                          ShortTrack sTrack, Track trackDoc, List<string> albumArtists, List<string> trackGenres)
        {
            // If artist name is nothing, set to "Unknown Artist"
            string artist = string.IsNullOrEmpty(artistName) ? "Unknown Artist" : artistName;

            // Try to find the artist
            var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", artist);
            var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();

            // If the artist was not found, create a new one
            if (artistDoc == null)
            {
                // Create Artist Object
                MelonScanner.CurrentStatus = $"Adding {artist}";
                artistDoc = new Artist()
                {
                    _id = aId,
                    ArtistId = aId.ToString(),
                    ArtistName = artist,
                    Bio = "",
                    Rating = 0,
                    DateAdded = DateTime.Now.ToUniversalTime(),
                    Releases = new List<ShortAlbum>(),
                    Genres = new List<string>(),
                    SeenOn = new List<ShortAlbum>(),
                    Tracks = new List<ShortTrack> { sTrack }
                };

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
                MelonScanner.CurrentStatus = $"Updating {artist}";

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
                    artistDoc.Tracks.RemoveAll(t => t.TrackId == trackDoc.TrackId);
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
        public static void CreateOrUpdateAlbum(ref Album albumDoc, Track trackDoc, MelonId AlbumId, ATL.Track fileMetadata, List<string> albumArtists,
                                               List<MelonId> AlbumArtistIds, List<string> trackArtists, List<MelonId> TrackArtistIds,
                                               List<string> trackGenres, ShortTrack sTrack, IMongoCollection<Album> AlbumCollection)
        {

            var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Album);
            albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumArtists[0]);

            if (albumDoc == null) // If albumDoc is null, make a new album.
            {
                // Set the album name to "Unkown album" if no album name is found.
                string albumName = string.IsNullOrEmpty(fileMetadata.Album) ? "Unknown Album" : fileMetadata.Album;

                MelonScanner.CurrentStatus = $"Adding {fileMetadata.Album}";

                // Create the new album object
                albumDoc = new Album
                {
                    _id = AlbumId,
                    AlbumId = AlbumId.ToString(),
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
                    using (FileStream artFile = new FileStream($"{StateManager.melonPath}/AlbumArts/{albumDoc.AlbumId}-{i}.jpg", FileMode.Create, System.IO.FileAccess.Write))
                    {

                        byte[] bytes = fileMetadata.EmbeddedPictures[i].PictureData;
                        artFile.Write(bytes, 0, bytes.Length);
                    }
                    albumDoc.AlbumArtPaths.Add($"{albumDoc.AlbumId}-{i}.jpg");
                }

                // Set the album's artists.
                for (int i = 0; i < albumArtists.Count(); i++)
                {
                    try
                    {
                        albumDoc.AlbumArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], ArtistId = AlbumArtistIds[i].ToString(), _id = AlbumArtistIds[i] });
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
                        albumDoc.ContributingArtists.Add(new ShortArtist() { ArtistName = trackArtists[i], ArtistId = TrackArtistIds[i].ToString(), _id = TrackArtistIds[i] });
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
                // Add artists to album artists if they are not already listed
                for (int i = 0; i < albumArtists.Count(); i++)
                {
                    try
                    {
                        if (!albumDoc.AlbumArtists.Any(artist => artist.ArtistId == AlbumArtistIds[i].ToString()))
                        {
                            albumDoc.AlbumArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], ArtistId = AlbumArtistIds[i].ToString(), _id = AlbumArtistIds[i] });
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
                        if (!albumDoc.ContributingArtists.Any(artist => artist.ArtistId == TrackArtistIds[i].ToString()))
                        {
                            albumDoc.ContributingArtists.Add(new ShortArtist() { ArtistName = trackArtists[i], ArtistId = TrackArtistIds[i].ToString(), _id = TrackArtistIds[i] });
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
                var existingTrack = albumDoc.Tracks.FirstOrDefault(t => t.TrackId == sTrack.TrackId);
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
        public static void CreateOrUpdateTrack(MelonId TrackId, ATL.Track fileMetadata, ShortAlbum sAlbum, List<string> albumArtists,
                                               List<string> trackArtists, List<MelonId> AlbumArtistIds, List<MelonId> TrackArtistIds, int count,
                                               List<string> trackGenres, Track trackDoc, IMongoCollection<Track> TracksCollection,
                                               IMongoCollection<Album> AlbumCollection, IMongoCollection<Artist> ArtistCollection)
        {
            // Create the new track object
            Track track = new Track
            {
                _id = TrackId,
                TrackId = TrackId.ToString(),
                LastModified = File.GetLastWriteTime(fileMetadata.Path).ToUniversalTime(),
                DateAdded = DateTime.UtcNow,
                TrackName = fileMetadata.Title ?? "Unknown",
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
                Rating = fileMetadata.Popularity ?? 0,
                TrackArtCount = fileMetadata.EmbeddedPictures?.Count() ?? 0,
                Duration = fileMetadata.DurationMs.ToString() ?? "",
                TrackArtists = new List<ShortArtist>(),
                TrackGenres = trackGenres ?? new List<string>(),
                ReleaseDate = fileMetadata.Date ?? DateTime.MinValue,
                LyricsPath = "",
            };

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
                    track.TrackArtists.Add(new ShortArtist() { _id = AlbumArtistIds[count - TrackArtistIds.Count()], ArtistId = AlbumArtistIds[count - TrackArtistIds.Count()].ToString(), ArtistName = albumArtists[count - TrackArtistIds.Count()] });
                }
                else
                {
                    track.TrackArtists.Add(new ShortArtist() { _id = TrackArtistIds[i], ArtistId = TrackArtistIds[i].ToString(), ArtistName = trackArtists[i] });
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
                track.TrackId = trackDoc._id.ToString();
                var trackfilter = Builders<Track>.Filter.Eq("Path", fileMetadata.Path);
                TracksCollection.ReplaceOne(trackfilter, track);

                // remove old tracks from old albums
                if (trackDoc.Album.AlbumId != track.Album.AlbumId)
                {
                    var aFilter = Builders<Album>.Filter.Eq(x => x.AlbumId, trackDoc.Album.AlbumId);
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
                        var aFilter = Builders<Artist>.Filter.Eq(x => x.ArtistId, a.ArtistId);
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
                    var filter = Builders<Track>.Filter.In(a => a.TrackId, albumDoc.Tracks.Select(x=>x.TrackId));
                    var fullTracks = TracksCollection.Find(filter).ToList();
                    albumDoc.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new ShortTrack() { _id = x._id, TrackId = x.TrackId, TrackName = x.TrackName }).ToList();
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
                    var trackFilter = Builders<Track>.Filter.In(a => a.TrackId, artistDoc.Tracks.Select(x => x.TrackId));
                    var fullTracks = TracksCollection.Find(trackFilter).ToList();
                    var rAlbumFilter = Builders<Album>.Filter.In(a => a.AlbumId, artistDoc.Releases.Select(x => x.AlbumId));
                    var fullReleases = AlbumCollection.Find(rAlbumFilter).ToList();
                    var sAlbumFilter = Builders<Album>.Filter.In(a => a.AlbumId, artistDoc.SeenOn.Select(x => x.AlbumId));
                    var fullSeenOn = AlbumCollection.Find(sAlbumFilter).ToList();
                    try { artistDoc.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x => new ShortTrack() { _id = x._id, TrackId = x.TrackId, TrackName = x.TrackName }).ToList(); } catch (Exception) { }
                    try { artistDoc.Releases = fullReleases.OrderBy(x => x.ReleaseDate).Select(x => new ShortAlbum() { _id = x._id, AlbumId = x.AlbumId, AlbumName = x.AlbumName }).ToList(); } catch (Exception) { }
                    try { artistDoc.SeenOn = fullSeenOn.OrderBy(x => x.ReleaseDate).Select(x => new ShortAlbum() { _id = x._id, AlbumId = x.AlbumId, AlbumName = x.AlbumName }).ToList(); } catch (Exception) { }
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
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Database Reset" });

            // Description
            Console.WriteLine($"This will remove all of the db entries.");
            Console.WriteLine($"It shouldn't take long but will require you to rescan your files.");
            Console.WriteLine($"Would you still like to reset?");
            var input = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            switch (input)
            {
                case "Yes":
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

                    var failedCollection = NewMelonDB.GetCollection<FailedFiles>("FailedFiles");
                    failedCollection.DeleteMany(Builders<FailedFiles>.Filter.Empty);

                    var statsCollection = NewMelonDB.GetCollection<FailedFiles>("Stats");
                    statsCollection.DeleteMany(Builders<FailedFiles>.Filter.Empty);

                    if (Directory.Exists($"{StateManager.melonPath}/AlbumArts/"))
                    {
                        foreach (var file in Directory.GetFiles($"{StateManager.melonPath}/AlbumArts/"))
                        {
                            File.Delete(file);
                        }
                    }
                    break;
                case "No":
                    return;
            }
        }
        public static void Scan()
        {
            if (Scanning)
            {
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Full Scan" });

                Console.WriteLine($"The scanner is already running, view progress?");
                var opt = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
                switch (opt)
                {
                    case "Yes":
                        ScanProgressView();
                        break;
                    case "No":
                        return;
                }
            }
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Full Scan" });

            // Description
            Console.WriteLine($"This will start a scan of all saved paths and their subdirectories.");
            Console.WriteLine($"It may {"take awhile".Pastel(MelonColor.Highlight)} depending on how many files you have.");
            Console.WriteLine($"Ready to Start?");
            var input = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            switch (input)
            {
                case "Yes":
                    if (!Scanning)
                    {
                        Thread scanThread = new Thread(MelonScanner.StartScan);
                        scanThread.Start(false);
                        DisplayManager.UIExtensions.Add(() => { Console.WriteLine("Library scan started!".Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                        //DisplayManager.MenuOptions.Remove("Library Scanner");
                        //DisplayManager.MenuOptions.Insert(0, "Scan Progress", ScanProgressView);
                    }
                    ScanProgressView();
                    break;
                case "No":
                    return;
            }
        }
        public static void ScanShort()
        {
            if (Scanning)
            {
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Short Scan" });

                Console.WriteLine($"The scanner is already running, view progress?");
                var opt = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
                switch (opt)
                {
                    case "Yes":
                        ScanProgressView();
                        break;
                    case "No":
                        return;
                }
            }
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Short Scan" });

            // Description
            Console.WriteLine($"The short scan will only scan recently updated files or files not already in the db.");
            Console.WriteLine($"It may {"take awhile".Pastel(MelonColor.Highlight)} depending on how many files you have.");
            Console.WriteLine($"Ready to Start?");
            var input = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            switch (input)
            {
                case "Yes":
                    if (Scanning)
                    {
                        Thread scanThread = new Thread(MelonScanner.StartScan);
                        scanThread.Start(true);
                        DisplayManager.UIExtensions.Add(() => { Console.WriteLine("Library scan started!".Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                        //DisplayManager.MenuOptions.Remove("Library Scanner");
                        //DisplayManager.MenuOptions.Insert(0, "Scan Progress", ScanProgressView);
                    }
                    ScanProgressView();
                    break;
                case "No":
                    return;
            }
        }
        public static void ScanProgressView()
        {
            // Title
            Console.CursorVisible = false;
            MelonUI.ClearConsole();
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scanner Progress" });

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
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scanner Progress" });
                    }
                    try
                    {
                        string controls = $"Ctrls: Esc(Back)";
                        int conX = Console.WindowWidth - controls.Length - 2;
                        Console.CursorLeft = conX;
                        Console.CursorTop = sTop;
                        Console.Write(controls.Pastel(MelonColor.BackgroundText));
                        Console.CursorTop = sTop;
                        Console.CursorLeft = sLeft;
                        Console.WriteLine($"Scanned {ScannedFiles.ToString().Pastel(MelonColor.Melon)} // {FoundFiles.ToString().Pastel(MelonColor.Melon)} Found");
                        MelonUI.DisplayProgressBar(ScannedFiles, FoundFiles, '#', '-');
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        string msg = $"Time Left: {TimeSpan.FromMilliseconds((averageMilliseconds / ScannedFiles)*(FoundFiles - ScannedFiles)).ToString(@"hh\:mm\:ss")}";
                        int max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Avg Time Per File: {TimeSpan.FromMilliseconds(averageMilliseconds / ScannedFiles)}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Current Folder: {CurrentFolder}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Current File: {CurrentFile}";
                        max = msg.Length >= Console.WindowWidth ? Console.WindowWidth - 4 : msg.Length;
                        Console.WriteLine(msg.Substring(0, max).Pastel(MelonColor.BackgroundText));
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                        msg = $"Status: {CurrentStatus}";
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
