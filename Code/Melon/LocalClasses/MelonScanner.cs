﻿using Amazon.Auth.AccessControlPolicy;
using Azure;
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
                ScanFolder(path, skip);
            }

            while (count != 0)
            {
                
            }

            CurrentFolder = "N/A";
            CurrentFile = "N/A";
            CurrentStatus = "Delete Pass, Finishing up~";
            DeletePass();
            endDisplay = true;
            CurrentFile = "N/A";
            CurrentStatus = "Complete!";
            IndexCollections();
            DisplayManager.UIExtensions.Clear();
        }
        private static void DeletePass()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");

            int page = 0;
            int count = 100;
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
                        var filter = Builders<Album>.Filter.Eq("_id", track.Album._id);
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
                            var aFilter = Builders<Artist>.Filter.Eq("_id", artist._id);
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
                        var tFilter = Builders<Track>.Filter.Eq("_id", track._id);
                        TracksCollection.DeleteOne(tFilter);
                    }

                }

                if(tracks.Count() != 100)
                {
                    break;
                }
                page++;
            }
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
                    var fileMetadata = new ATL.Track(file);//TagLib.File.Create(file);
                    //var fileMetadataNew = new ATL.Track(file); //TagLib.File.Create(file);
                    List<string> albumArtists = new List<string>();

                    // TODO: Make this a setting
                    albumArtists.AddRange(fileMetadata.AlbumArtist.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries));
                    List<string> trackArtists = new List<string>();

                    // TODO: Make this a setting too
                    trackArtists.AddRange(fileMetadata.Artist.Split(new string[] { ",", ";", "/", "feat.", "ft." }, StringSplitOptions.TrimEntries));

                    // Split Genres
                    List<string> trackGenres = new List<string>();
                    // TODO: Make this a setting too
                    trackGenres.AddRange(fileMetadata.Genre.Split(new string[] { ",", ";", "/", }, StringSplitOptions.TrimEntries));

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
                        var artistFilter = Builders<Artist>.Filter.Empty;
                        if (trackArtists.Count() < num)
                        {
                            artistFilter = Builders<Artist>.Filter.Eq("ArtistName", albumArtists[i]);
                        }
                        else
                        {
                            artistFilter = Builders<Artist>.Filter.Eq("ArtistName", trackArtists[i]);
                        }
                        var artistDoc = ArtistCollection.Find(artistFilter).FirstOrDefault();


                        if (artistDoc == null)
                        {
                            ArtistIds.Add(ObjectId.GenerateNewId());
                        }
                        else
                        {
                            ArtistIds.Add(artistDoc._id);
                        }
                    }
                    var albumFilter = Builders<Album>.Filter.Eq("AlbumName", fileMetadata.Album);
                    albumFilter = albumFilter & Builders<Album>.Filter.AnyStringIn("AlbumArtists.ArtistName", albumArtists[0]);
                    var albumDoc = AlbumCollection.Find(albumFilter).FirstOrDefault();

                    ObjectId AlbumId = ObjectId.GenerateNewId();
                    if (albumDoc != null)
                    {
                        AlbumId = albumDoc._id;
                    }
                    ObjectId TrackId = ObjectId.GenerateNewId();

                    int count = 0;
                    ShortAlbum sAlbum = new ShortAlbum()
                    {
                        _id = AlbumId,
                        AlbumId = AlbumId.ToString(),
                        AlbumName = fileMetadata.Album,
                    };
                    try { sAlbum.ReleaseType = fileMetadata.AdditionalFields["RELEASETYPE"]; } catch (Exception) { sAlbum.ReleaseType = ""; }
                    try { sAlbum.ReleaseDate = fileMetadata.Date.Value; } catch (Exception) { }

                    ShortTrack sTrack = new ShortTrack()
                    {
                        _id = TrackId,
                        TrackId = TrackId.ToString(),
                        Album = sAlbum,
                        Duration = fileMetadata.DurationMs.ToString(),
                        Position = fileMetadata.TrackNumber.Value,
                        Disc = fileMetadata.DiscNumber.Value,
                        TrackArtCount = fileMetadata.EmbeddedPictures.Count(),
                        TrackName = fileMetadata.Title,
                        Path = fileMetadata.Path,
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
                                Rating = 0,
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
                                             where release.AlbumName == fileMetadata.Album
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
                                             where release.AlbumName == fileMetadata.Album
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
                                         where t.Path == fileMetadata.Path
                                         select t;
                            if (tQuery.Count() != 0)
                            {
                                artistDoc.Tracks.Remove(tQuery.FirstOrDefault());
                            }

                            artistDoc.Tracks.Add(sTrack);
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
                        if (albumDoc == null)
                        {
                            CurrentStatus = $"Adding {fileMetadata.Album}";
                            Album album = new Album();
                            album._id = AlbumId;
                            album.AlbumId = AlbumId.ToString();
                            album.AlbumName = fileMetadata.Album;
                            try { album.Bio = ""; } catch (Exception) { }
                            try { album.TotalDiscs = fileMetadata.DiscTotal.Value; } catch (Exception) { album.TotalDiscs = 1; }
                            try { album.TotalTracks = fileMetadata.TrackTotal.Value; } catch (Exception) { album.TotalTracks = 0; }
                            try 
                            {
                                if (fileMetadata.Publisher == null)
                                {
                                    album.Publisher = "";
                                }
                                else
                                {
                                    album.Publisher = fileMetadata.Publisher;
                                }
                            } catch (Exception) { album.Publisher = ""; }
                            try 
                            {
                                if (fileMetadata.AdditionalFields["RELEASESTATUS"] == null)
                                {
                                    album.ReleaseStatus = "";
                                }
                                else
                                {
                                    album.ReleaseStatus = fileMetadata.AdditionalFields["RELEASESTATUS"];
                                }
                            } catch (Exception) { album.ReleaseStatus = ""; }
                            try 
                            { 
                                if (fileMetadata.AdditionalFields["RELEASETYPE"] == null)
                                {
                                    album.ReleaseType = "";
                                }
                                else
                                {
                                    album.ReleaseType = fileMetadata.AdditionalFields["RELEASETYPE"];
                                }
                            } catch (Exception) { album.ReleaseType = ""; }
                            try { album.ReleaseDate = fileMetadata.Date.Value; } catch (Exception) { }
                            album.AlbumArtPaths = new List<string>();
                            album.Tracks = new List<ShortTrack>();
                            album.AlbumArtists = new List<ShortArtist>();
                            album.AlbumGenres = new List<string>();
                            try
                            {
                                album.Rating = 0;
                            }
                            catch (Exception) { }
                            albumDoc = album;


                            for (int i = 0; i < fileMetadata.EmbeddedPictures.Count(); i++)
                            {
                                using (FileStream artFile = new FileStream($"{StateManager.melonPath}/AlbumArts/{album._id}-{i}.jpg", FileMode.Create, System.IO.FileAccess.Write))
                                {

                                    byte[] bytes = fileMetadata.EmbeddedPictures[i].PictureData;
                                    artFile.Write(bytes, 0, bytes.Length);
                                }
                                album.AlbumArtPaths.Add($"{album._id}-{i}.jpg");
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
                                         where release.TrackName == fileMetadata.Title
                                         select release;
                            if (tQuery.Count() == 0)
                            {
                                var query = (from t in albumDoc.Tracks
                                             where t.Path == fileMetadata.Path
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
                        try 
                        {
                            if (fileMetadata.Title == null)
                            {
                                track.TrackName = "Uknown";
                            }
                            else
                            {
                                track.TrackName = fileMetadata.Title;
                            }
                        } catch (Exception) { track.TrackName = "Uknown"; }
                        try { track.Album = sAlbum; } catch (Exception) { }
                        try { track.Path = fileMetadata.Path; } catch (Exception) { }
                        try { track.Position = fileMetadata.TrackNumber.Value; } catch (Exception) { track.Position = 0; }
                        try { track.Format = Path.GetExtension(file); } catch (Exception) { track.Format = ""; }
                        try { track.Bitrate = fileMetadata.Bitrate.ToString(); } catch (Exception) { track.Bitrate = ""; }
                        try { track.SampleRate = fileMetadata.SampleRate.ToString(); } catch (Exception) { track.SampleRate = ""; }
                        try 
                        {
                            if (fileMetadata.ChannelsArrangement != null)
                            {
                                track.Channels = fileMetadata.ChannelsArrangement.NbChannels.ToString();
                            }
                        } 
                        catch (Exception) { track.Channels = ""; }
                        try { track.BitsPerSample = fileMetadata.BitDepth.ToString(); } catch (Exception) { track.BitsPerSample = ""; }
                        try { track.Disc = fileMetadata.DiscNumber.Value; } catch (Exception) { track.Disc = 1; }
                        try 
                        {
                            if (fileMetadata.AdditionalFields["MUSICBRAINZ_RELEASETRACKID"] == null)
                            {
                                track.MusicBrainzID = "";
                            }
                            else
                            {
                                track.MusicBrainzID = fileMetadata.AdditionalFields["MUSICBRAINZ_RELEASETRACKID"];
                            }
                            
                        } catch (Exception) { track.MusicBrainzID = ""; }
                        try 
                        {
                            if (fileMetadata.AdditionalFields["ISRC"] == null)
                            {
                                track.ISRC = "";
                            }
                            else
                            {
                                track.ISRC = fileMetadata.AdditionalFields["ISRC"];
                            }
                        } catch (Exception) { track.ISRC = ""; }
                        try 
                        {
                            if (fileMetadata.Year == null)
                            {
                                track.Year = "";
                            }
                            else
                            {
                                track.Year = fileMetadata.Year.Value.ToString();
                            }
                        } catch (Exception) { track.Year = ""; }
                        try
                        {
                            track.Rating = fileMetadata.Popularity.Value;
                        }
                        catch (Exception) {  }
                        try { track.TrackArtCount = fileMetadata.EmbeddedPictures.Count(); } catch (Exception) { track.TrackArtCount = 0; }
                        try { track.Duration = fileMetadata.DurationMs.ToString(); } catch (Exception) { track.Duration = ""; }
                        try { track.TrackArtists = new List<ShortArtist>(); } catch (Exception) { }
                        try { track.TrackGenres = new List<string>(); } catch (Exception) { }
                        try { track.ReleaseDate = fileMetadata.Date.Value; } catch (Exception) { }

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

                            // remove old tracks from old albums
                            if (trackDoc.Album._id != track.Album._id)
                            {
                                var aFilter = Builders<Album>.Filter.Eq("_id", trackDoc.Album._id);
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
                                    var aFilter = Builders<Artist>.Filter.Eq("_id", trackDoc.Album._id);
                                    var aritst = ArtistCollection.Find(aFilter).ToList();
                                    if (aritst.Count() != 0)
                                    {
                                        aritst[0].Tracks.Remove(new ShortTrack(trackDoc));
                                    }
                                }
                            }
                        }
                        count++;
                    }
                }
                catch (Exception e)
                {
                    if(e.Message.Contains("DuplicateKey"))
                    {
                        ScannedFiles++;
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
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Short Scan" });

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
