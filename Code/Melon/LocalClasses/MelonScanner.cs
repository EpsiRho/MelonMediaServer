using Amazon.Auth.AccessControlPolicy;
using Melon.Classes;
using Melon.DisplayClasses;
using Melon.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Pastel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
        private static List<string> FoundPaths { get; set; }

        // Scanning Functions
        public static void StartScan(object skipBool)
        {
            bool skip = (bool)skipBool;
            ScannedFiles = 0;
            FoundFiles = 0;
            averageMilliseconds = 0;
            Indexed = false;
            MelonUI.ClearConsole();
            if(StateManager.MelonSettings.LibraryPaths.Count() == 0)
            {
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

            int count = 0;
            foreach(var path in StateManager.MelonSettings.LibraryPaths)
            {
                ScanFolder(path, true);
            }

            while (count != 0)
            {
                
            }

            endDisplay = true;
            CurrentFolder = "N/A";
            CurrentFile = "N/A";
            CurrentStatus = "Scanning Complete";
            IndexCollections();
            DisplayManager.UIExtensions.Clear();
        }
        private static void ScanFolderCounter(string path)
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
            var folders = Directory.GetDirectories(path);
            foreach (var folder in folders)
            {
                ScanFolder(folder, skip);
            }

            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var FailedCollection = NewMelonDB.GetCollection<FailedFiles>("FailedFiles");

            var files = Directory.GetFiles(path);
            Stopwatch watch = new Stopwatch();
            foreach(var file in files)
            {
                watch.Restart();
                try
                {
                    CurrentFile = file;
                    var filename = Path.GetFileName(file);
                    if (!filename.EndsWith(".flac") && !filename.EndsWith(".aac") && !filename.EndsWith(".wma") &&
                        !filename.EndsWith(".wav") && !filename.EndsWith(".mp3") && !filename.EndsWith(".m4a"))
                    {
                        ScannedFiles++;
                        continue;
                    }

                    Track trackDoc = null;
                    var trackfilter = Builders<Track>.Filter.Empty;
                    trackfilter = trackfilter & Builders<Track>.Filter.Eq("Path", file);
                    trackDoc = TracksCollection.Find(trackfilter).FirstOrDefault();
                    if (skip)
                    {
                        if (trackDoc != null)
                        {
                            DateTime lastModified = System.IO.File.GetLastWriteTime(file).ToUniversalTime();
                            if(trackDoc.LastModified.ToString("MM/dd/yyyy hh:mm:ss") == lastModified.ToString("MM/dd/yyyy hh:mm:ss"))
                            {
                                ScannedFiles++;
                                continue;
                            }
                        }
                    }



                    CurrentStatus = "Preparing Artist and Genre tags";

                    // Split artists
                    var fileMetadata = TagLib.File.Create(file);
                    List<string> albumArtists = new List<string>();
                    foreach (var aa in fileMetadata.Tag.AlbumArtists)
                    {
                        // TODO: Make this a setting
                        albumArtists.AddRange(aa.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries));
                    }
                    List<string> trackArtists = new List<string>();
                    foreach (var ta in fileMetadata.Tag.Performers)
                    {
                        // TODO: Make this a setting too
                        // ps. 9 in the am pm now
                        trackArtists.AddRange(ta.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries));
                    }

                    // Split Genres
                    List<string> trackGenres = new List<string>();
                    foreach (var tg in fileMetadata.Tag.Genres)
                    {
                        // TODO: Make this a setting too 2
                        trackGenres.AddRange(tg.Split(new string[] { ",", ";", "/", }, StringSplitOptions.TrimEntries));
                    }

                    // Conform Genres
                    //foreach (var tg in trackGenres)
                    //{

                    //}

                    // Generate IDs
                    List<ObjectId> ArtistIds = new List<ObjectId>();
                    int num = trackArtists.Count();
                    if (num < albumArtists.Count())
                    {
                        num = albumArtists.Count();
                    }
                    for (int i = 0; i < num; i++)
                    {
                        ArtistIds.Add(ObjectId.GenerateNewId());
                    }
                    ObjectId AlbumId = ObjectId.GenerateNewId();
                    ObjectId TrackId = ObjectId.GenerateNewId();

                    int count = 0;
                    ShortAlbum sAlbum = new ShortAlbum()
                    {
                        _id = AlbumId,
                        AlbumId = AlbumId.ToString(),
                        AlbumName = fileMetadata.Tag.Album,
                        ReleaseType = fileMetadata.Tag.MusicBrainzReleaseType
                    };
                    try { sAlbum.ReleaseDate = DateTime.Parse(fileMetadata.Tag.Year.ToString()); } catch (Exception) { }

                    ShortTrack sTrack = new ShortTrack()
                    {
                        _id = TrackId,
                        TrackId = TrackId.ToString(),
                        Album = sAlbum,
                        Duration = fileMetadata.Properties.Duration.ToString(),
                        Position = fileMetadata.Tag.Track,
                        TrackName = fileMetadata.Tag.Title,
                        Path = path,
                        TrackArtists = new List<ShortArtist>()
                    };
                    for (int i = 0; i < trackArtists.Count(); i++)
                    {
                        sTrack.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistId = ArtistIds[i].ToString(), ArtistName = trackArtists[i] });
                    }

                    foreach (var artist in trackArtists)
                    {
                        var artistFilter = Builders<Artist>.Filter.Eq("ArtistName", artist);
                        var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();


                        if (artistDoc == null)
                        {
                            // Create Artist Object
                            CurrentStatus = $"Adding {artist}";
                            artistDoc = new Artist()
                            {
                                ArtistName = artist,
                                Bio = "",
                                ArtistPfp = "",
                                _id = ArtistIds[count],
                                ArtistId = ArtistIds[count].ToString(),
                                Releases = new List<ShortAlbum>(),
                                Genres = new List<string>(),
                                SeenOn = new List<ShortAlbum>(),
                                Tracks = new List<ShortTrack>()
                            };


                            // Add the first release
                            if (albumArtists.Contains(artist))
                            {
                                artistDoc.Releases.Add(sAlbum);
                            }
                            else
                            {
                                artistDoc.SeenOn.Add(sAlbum);
                            }


                            for (int i = 0; i < trackArtists.Count(); i++)
                            {
                                sTrack.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistId = ArtistIds[i].ToString(), ArtistName = trackArtists[i] });
                            }
                            artistDoc.Tracks.Add(sTrack);

                            ArtistCollection.InsertOne(artistDoc);
                        }
                        else
                        {
                            CurrentStatus = $"Updating {artist}";
                            ArtistIds[count] = artistDoc._id;


                            if (albumArtists.Contains(artist))
                            {
                                var rQuery = from release in artistDoc.Releases
                                             where release.AlbumName == fileMetadata.Tag.Album
                                             select release;
                                if (rQuery.Count() == 0)
                                {
                                    artistDoc.Releases.Add(sAlbum);
                                    if (trackDoc != null)
                                    {
                                        artistDoc.Releases.Remove(trackDoc.Album);

                                    }

                                    ArtistCollection.ReplaceOne(artistFilter, artistDoc);
                                }
                            }
                            else
                            {
                                var sQuery = from release in artistDoc.SeenOn
                                             where release.AlbumName == fileMetadata.Tag.Album
                                             select release;
                                if (sQuery.Count() == 0)
                                {
                                    artistDoc.SeenOn.Add(sAlbum);
                                    if (trackDoc != null)
                                    {
                                        artistDoc.SeenOn.Remove(trackDoc.Album);

                                    }

                                }
                            }

                            var tQuery = from t in artistDoc.Tracks
                                         where t.TrackName == fileMetadata.Tag.Title
                                         select t;
                            if (tQuery.Count() == 0)
                            {
                                var query = (from t in artistDoc.Tracks
                                             where t.Path == path
                                             select t).FirstOrDefault();
                                if (query != null)
                                {
                                    artistDoc.Tracks.Remove(query);
                                }
                                artistDoc.Tracks.Add(sTrack);

                            }
                            foreach (var genre in trackGenres)
                            {
                                if (!artistDoc.Genres.Contains(genre))
                                {
                                    artistDoc.Genres.Add(genre);
                                }
                            }

                            ArtistCollection.ReplaceOne(artistFilter, artistDoc);

                        }

                        // Add Release
                        var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Tag.Album);
                        try
                        {
                            albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumArtists[0]);
                        }
                        catch (Exception)
                        {
                            //albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", fileMetadata.Tag.FirstPerformer);
                        }
                        var albumDoc = AlbumCollection.Find(albumFilter).FirstOrDefault();

                        if (albumDoc == null)
                        {
                            CurrentStatus = $"Adding {fileMetadata.Tag.Album}";
                            Album album = new Album();
                            album._id = AlbumId;
                            album.AlbumId = AlbumId.ToString();
                            album.AlbumName = fileMetadata.Tag.Album;
                            try { album.Bio = ""; } catch (Exception) { }
                            try { album.TotalDiscs = fileMetadata.Tag.DiscCount; } catch (Exception) { }
                            try { album.TotalTracks = fileMetadata.Tag.TrackCount; } catch (Exception) { }
                            try { album.Publisher = fileMetadata.Tag.Publisher; } catch (Exception) { }
                            try { album.ReleaseStatus = fileMetadata.Tag.MusicBrainzReleaseStatus; } catch (Exception) { }
                            try { album.ReleaseType = fileMetadata.Tag.MusicBrainzReleaseType; } catch (Exception) { }
                            try { album.ReleaseDate = DateTime.Parse(fileMetadata.Tag.Year.ToString()); } catch (Exception) { }
                            album.AlbumArtPaths = new List<string>();
                            album.Tracks = new List<ShortTrack>();
                            album.AlbumArtists = new List<ShortArtist>();
                            album.AlbumGenres = new List<string>();
                            albumDoc = album;


                            for (int i = 0; i < fileMetadata.Tag.Pictures.Length; i++)
                            {
                                using (FileStream artFile = new FileStream($"{StateManager.melonPath}/AlbumArts/{album._id}-{i}.jpg", FileMode.Create, System.IO.FileAccess.Write))
                                {

                                    byte[] bytes = fileMetadata.Tag.Pictures[i].Data.Data;
                                    artFile.Write(bytes, 0, bytes.Length);
                                }
                                album.AlbumArtPaths.Add($"{StateManager.melonPath}/AlbumArts/{album._id}-{i}.jpg");
                            }

                            for (int i = 0; i < albumArtists.Count(); i++)
                            {
                                try
                                {
                                    albumDoc.AlbumArtists.Add(new ShortArtist() { ArtistName = albumArtists[i], ArtistId = ArtistIds[i].ToString(), _id = ArtistIds[i] });
                                }
                                catch (Exception e)
                                {

                                }
                            }
                            foreach (var genre in trackGenres)
                            {
                                albumDoc.AlbumGenres.Add(genre);
                            }
                            albumDoc.Tracks.Add(sTrack);
                            try
                            {
                                AlbumCollection.InsertOne(albumDoc);
                            }
                            catch (Exception e)
                            {

                            }
                        }
                        else
                        {
                            if (trackDoc != null)
                            {
                                foreach (var art in trackDoc.TrackArtists)
                                {
                                    albumDoc.AlbumArtists.Remove(art);
                                }
                            }
                            for (int i = 0; i < trackArtists.Count(); i++)
                            {
                                var aQuery = from release in albumDoc.AlbumArtists
                                             where release.ArtistName == trackArtists[i]
                                             select release;
                                if (aQuery.Count() == 0)
                                {
                                    albumDoc.AlbumArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistId = ArtistIds[i].ToString(), ArtistName = trackArtists[i] });
                                }
                            }
                            foreach (var genre in trackGenres)
                            {
                                if (!albumDoc.AlbumGenres.Contains(genre))
                                {
                                    var arrayUpdateGenres = Builders<Album>.Update.Push("AlbumGenres", genre);
                                    AlbumCollection.UpdateOne(albumFilter, arrayUpdateGenres);
                                }
                            }

                            var tQuery = from release in albumDoc.Tracks
                                         where release.TrackName == fileMetadata.Tag.Title
                                         select release;
                            if (tQuery.Count() == 0)
                            {
                                var query = (from t in albumDoc.Tracks
                                             where t.Path == path
                                             select t).FirstOrDefault();
                                if (query != null)
                                {
                                    albumDoc.Tracks.Remove(query);
                                }

                                albumDoc.Tracks.Add(sTrack);
                            }

                            AlbumCollection.ReplaceOne(albumFilter, albumDoc);

                        }

                        // Add Track
                        //var trackFilter = Builders<Track>.Filter.Empty;

                        //trackFilter = trackFilter & Builders<Track>.Filter.Eq("Path", file);
                        //trackDoc = TracksCollection.Find(trackFilter).FirstOrDefault();
                        Track track = new Track();
                        track._id = TrackId;
                        track.TrackId = TrackId.ToString();
                        track.LastModified = System.IO.File.GetLastWriteTime(file).ToUniversalTime();
                        try { track.TrackName = fileMetadata.Tag.Title; } catch (Exception) { }
                        try { track.Album = sAlbum; } catch (Exception) { }
                        try { track.Path = file; } catch (Exception) { }
                        try { track.Position = fileMetadata.Tag.Track; } catch (Exception) { }
                        try { track.Format = Path.GetExtension(file); } catch (Exception) { }
                        try { track.Bitrate = fileMetadata.Properties.AudioBitrate.ToString(); } catch (Exception) { }
                        try { track.SampleRate = fileMetadata.Properties.AudioSampleRate.ToString(); } catch (Exception) { }
                        try { track.Channels = fileMetadata.Properties.AudioChannels.ToString(); } catch (Exception) { }
                        try { track.BitsPerSample = fileMetadata.Properties.BitsPerSample.ToString(); } catch (Exception) { }
                        try { track.Disc = fileMetadata.Tag.Disc; } catch (Exception) { }
                        try { track.MusicBrainzID = fileMetadata.Tag.MusicBrainzTrackId; } catch (Exception) { }
                        try { track.ISRC = fileMetadata.Tag.ISRC; } catch (Exception) { }
                        try { track.Year = fileMetadata.Tag.Year.ToString(); } catch (Exception) { }
                        try { track.TrackArtCount = fileMetadata.Tag.Pictures.Length; } catch (Exception) { }
                        try { track.Duration = fileMetadata.Properties.Duration.ToString(); } catch (Exception) { }
                        try { track.TrackArtists = new List<ShortArtist>(); } catch (Exception) { }
                        try { track.TrackGenres = new List<string>(); } catch (Exception) { }
                        try { track.ReleaseDate = DateTime.Parse(fileMetadata.Tag.Year.ToString()); } catch (Exception) { }

                        for (int i = 0; i < trackArtists.Count(); i++)
                        {
                            track.TrackArtists.Add(new ShortArtist() { _id = ArtistIds[i], ArtistId = ArtistIds[i].ToString(), ArtistName = trackArtists[i] });
                        }
                        foreach (var genre in trackGenres)
                        {
                            track.TrackGenres.Add(genre);
                        }

                        if (trackDoc == null)
                        {
                            TracksCollection.InsertOne(track);
                        }
                        else
                        {
                            track._id = trackDoc._id;
                            track.TrackId = trackDoc._id.ToString();
                            TracksCollection.ReplaceOne(trackfilter, track);
                        }
                        count++;
                    }
                }
                catch (Exception e)
                {
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
            var artistIndexModel = new CreateIndexModel<BsonDocument>(trackIndexKeysDefinition, indexOptions);
            ArtistCollection.Indexes.CreateOne(artistIndexModel);

            var albumIndexKeysDefinition = Builders<BsonDocument>.IndexKeys.Ascending("AlbumName");
            var AlbumCollection = NewMelonDB.GetCollection<BsonDocument>("Albums");
            var albumIndexModel = new CreateIndexModel<BsonDocument>(trackIndexKeysDefinition, indexOptions);
            AlbumCollection.Indexes.CreateOne(albumIndexModel);
            


        }

        // UI
        public static void Scan()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scan" });

            // Description
            Console.WriteLine($"This will start a scan of all saved paths and their subdirectories.");
            Console.WriteLine($"It may {"take awhile".Pastel(MelonColor.Highlight)} depending on how many files you have.");
            Console.WriteLine($"Ready to Start?");
            var input = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            switch (input)
            {
                case "Yes":
                    Thread scanThread = new Thread(MelonScanner.StartScan);
                    scanThread.Start(false);
                    DisplayManager.UIExtensions.Add(() => { Console.WriteLine("Library scan started!".Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                    //DisplayManager.MenuOptions.Remove("Library Scanner");
                    //DisplayManager.MenuOptions.Insert(0, "Scan Progress", ScanProgressView);
                    ScanProgressView();
                    break;
                case "No":
                    return;
            }
        }
        public static void ScanShort()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Scan" });

            // Description
            Console.WriteLine($"The short scan will only scan recently updated files or files not already in the db.");
            Console.WriteLine($"It may {"take awhile".Pastel(MelonColor.Highlight)} depending on how many files you have.");
            Console.WriteLine($"Ready to Start?");
            var input = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            switch (input)
            {
                case "Yes":
                    Thread scanThread = new Thread(MelonScanner.StartScan);
                    scanThread.Start(true);
                    DisplayManager.UIExtensions.Add(() => { Console.WriteLine("Library scan started!".Pastel(MelonColor.Highlight)); DisplayManager.UIExtensions.RemoveAt(0); });
                    //DisplayManager.MenuOptions.Remove("Library Scanner");
                    //DisplayManager.MenuOptions.Insert(0, "Scan Progress", ScanProgressView);
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
                        string controls = $"Ctrls: Esc";
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
                        string msg = $"Time Left: {TimeSpan.FromMilliseconds((averageMilliseconds / ScannedFiles)*(FoundFiles - ScannedFiles)).ToString()}";
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
                    
            }

        }
    }
}
