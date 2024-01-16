using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/")]
    public class GeneralController : ControllerBase
    {
        private readonly ILogger<GeneralController> _logger;

        public GeneralController(ILogger<GeneralController> logger)
        {
            _logger = logger;
        }

        // Tracks
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("track")]
        public ObjectResult GetTrack(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);

            var trackDocs = TracksCollection.Find(trackFilter)
                                            .ToList();

            var track = trackDocs.FirstOrDefault();
            if(track == null)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            return new ObjectResult(track) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("update-track")]
        public ObjectResult UpdateTrack(string trackId, string disc = "", string isrc = "", string releaseDate = "", string position = "",
                                        [FromQuery] string[] trackGenres = null, string trackName = "", string year = "",
                                        string albumId = "", [FromQuery] string[] artistIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            
            var trackFilter = Builders<Track>.Filter.Eq(x=>x.TrackId, trackId);
            var foundTrack = TracksCollection.Find(trackFilter).FirstOrDefault();
            if(foundTrack == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            Album newAlbum = null;
            ShortAlbum newShortAlbum = null;
            List<Artist> newArtists = new List<Artist>();
            List<ShortArtist> newShortArtists = new List<ShortArtist>();
            if(albumId != "")
            {
                var aFilter = Builders<Album>.Filter.Eq(x=>x.AlbumId, albumId);
                newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                if (newAlbum == null)
                {
                    return new ObjectResult("Album Not Found") { StatusCode = 404 };
                }
                newShortAlbum = new ShortAlbum(newAlbum);
            }
            else
            {
                newShortAlbum = foundTrack.Album;
            }

            if(artistIds != null)
            {
                foreach (var id in artistIds)
                {
                    var aFilter = Builders<Artist>.Filter.Eq(x => x.ArtistId, id);
                    var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                    if (newArtist == null)
                    {
                        return new ObjectResult("Artist Not Found") { StatusCode = 404 };
                    }
                    newArtists.Add(newArtist);
                    newShortArtists.Add(new ShortArtist(newArtist));
                }
            }
            else
            {
                newShortArtists.AddRange(foundTrack.TrackArtists);
            }

            disc = disc == "" ? foundTrack.Disc.ToString() : disc;
            isrc = isrc == "" ? foundTrack.ISRC : isrc;
            releaseDate = releaseDate == "" ? foundTrack.ReleaseDate.ToString() : releaseDate;
            position = position == "" ? foundTrack.Position.ToString() : position;
            trackName = trackName == "" ? foundTrack.TrackName.ToString() : trackName;
            year = year == "" ? foundTrack.Year : year;

            if(trackGenres == null)
            {
                trackGenres = foundTrack.TrackGenres.ToArray();
            }


            // Create new objects
            var newTrack = new Track()
            {
                _id = foundTrack._id,
                TrackId = foundTrack.TrackId,
                Album = newShortAlbum,
                Bitrate = foundTrack.Bitrate,
                BitsPerSample = foundTrack.BitsPerSample,
                Channels = foundTrack.Channels,
                Disc = Convert.ToInt32(disc),
                Format = foundTrack.Format,
                ISRC = foundTrack.ISRC,
                MusicBrainzID = foundTrack.MusicBrainzID,
                Rating = foundTrack.Rating,
                ReleaseDate = DateTime.Parse(releaseDate),
                SampleRate = foundTrack.SampleRate,
                LyricsPath = foundTrack.LyricsPath,
                ServerURL = foundTrack.ServerURL,
                Position = Convert.ToInt32(position),
                TrackArtists = newShortArtists,
                TrackGenres = trackGenres.ToList(),
                TrackName = trackName,
                Year = year,
                Duration = foundTrack.Duration,
                DateAdded = foundTrack.DateAdded,
                LastModified = foundTrack.LastModified,
                Path = foundTrack.Path,
                PlayCount = foundTrack.PlayCount,
                SkipCount = foundTrack.SkipCount,
                TrackArtCount = foundTrack.TrackArtCount
            };

            var newShortTrack = new ShortTrack()
            {
                _id = foundTrack._id,
                TrackId = foundTrack.TrackId,
                TrackName = trackName,
                Disc = Convert.ToInt32(disc),
                ReleaseDate = DateTime.Parse(releaseDate),
                Album = newShortAlbum,
                TrackArtists = newShortArtists,
                Position = Convert.ToInt32(position),
                ServerURL = foundTrack.ServerURL,
                Duration = foundTrack.Duration,
                Path = foundTrack.Path,
                TrackArtCount = foundTrack.TrackArtCount
            };

            // Replace objects
            TracksCollection.ReplaceOne(trackFilter, newTrack);

            var albumFilter = Builders<Album>.Filter.ElemMatch(x=>x.Tracks, Builders<ShortTrack>.Filter.Eq(x=>x.TrackId, foundTrack.TrackId));
            var artistFilter = Builders<Artist>.Filter.ElemMatch(x=>x.Tracks, Builders<ShortTrack>.Filter.Eq(x=>x.TrackId, foundTrack.TrackId));
            var albumnUpdate = Builders<Album>.Update.Set("Tracks.$", newShortTrack);
            var artistUpdate = Builders<Artist>.Update.Set("Tracks.$", newShortTrack);

            AlbumsCollection.UpdateMany(albumFilter, albumnUpdate);
            ArtistsCollection.UpdateMany(artistFilter, artistUpdate);

            // Update File
            var fileMetadata = new ATL.Track(foundTrack.Path);

            fileMetadata.DiscNumber = disc == "" ? fileMetadata.DiscNumber : Convert.ToInt32(disc);
            if (!fileMetadata.AdditionalFields.ContainsKey("ISRC"))
            {
                fileMetadata.AdditionalFields.Add("ISRC", isrc);
            }
            fileMetadata.AdditionalFields["ISRC"] = isrc == "" ? fileMetadata.AdditionalFields["ISRC"] : isrc;
            fileMetadata.Date = releaseDate == "" ? fileMetadata.Date : DateTime.Parse(releaseDate);
            fileMetadata.TrackNumber = position == "" ? fileMetadata.TrackNumber : Convert.ToInt32(position);
            fileMetadata.Title = trackName == "" ? fileMetadata.Title : trackName;
            fileMetadata.Year = year == "" ? fileMetadata.Year : Convert.ToInt32(year);

            if (trackGenres != null)
            {
                string genreStr = "";
                foreach (var genre in trackGenres)
                {
                    genreStr += $"{genre};";
                }
                genreStr = genreStr.Substring(0, genreStr.Length - 1);
                fileMetadata.Genre = genreStr;
            }

            if(newAlbum != null)
            {
                fileMetadata.Album = newAlbum.AlbumName;
                fileMetadata.DiscTotal = newAlbum.TotalDiscs;
                fileMetadata.TrackTotal = newAlbum.TotalDiscs;
                fileMetadata.Publisher = newAlbum.Publisher;
                if (!fileMetadata.AdditionalFields.ContainsKey("RELEASESTATUS"))
                {
                    fileMetadata.AdditionalFields.Add("RELEASESTATUS", newAlbum.ReleaseStatus);
                }
                fileMetadata.AdditionalFields["RELEASESTATUS"] = newAlbum.ReleaseStatus;
                if (!fileMetadata.AdditionalFields.ContainsKey("ISRELEASETYPERC"))
                {
                    fileMetadata.AdditionalFields.Add("RELEASETYPE", newAlbum.ReleaseType);
                }
                fileMetadata.AdditionalFields["RELEASETYPE"] = newAlbum.ReleaseType;
                string artistStr = "";
                foreach (var artist in newAlbum.AlbumArtists)
                {
                    artistStr = $"{artist};";
                }
                artistStr = artistStr.Substring(0, artistStr.Length - 1);
                fileMetadata.AlbumArtist = artistStr;
            }

            if(newArtists.Count() != 0)
            {
                string artistStr = "";
                foreach (var artist in newArtists)
                {
                    artistStr += $"{artist.ArtistName};";
                }
                artistStr = artistStr.Substring(0, artistStr.Length - 1);
                fileMetadata.Artist = artistStr;
            }

            fileMetadata.Save();

            return new ObjectResult("Track updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult GetTracks([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            List<Track> tracks = new List<Track>();
            foreach(var id in ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);
                var track = TracksCollection.Find(trackFilter).FirstOrDefault();
                if(track != null)
                {
                    tracks.Add(track);
                }
            }


            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        // Albums
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album")]
        public ObjectResult GetAlbum(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);

            var albumDocs = AlbumsCollection.Find(albumFilter)
                                            .ToList();

            var album = albumDocs.FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }
            album.Tracks = null;

            return new ObjectResult(album) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album/tracks")]
        public ObjectResult GetAlbumTracks(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);

            var albumDocs = AlbumsCollection.Find(albumFilter)
                                            .ToList();

            var album = albumDocs.FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }
            List<Track> tracks = new List<Track>();
            for(uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Track>.Filter.Eq(x => x.TrackId, album.Tracks[i].TrackId);
                    var fullTrack = TracksCollection.Find(filter).FirstOrDefault();
                    tracks.Add(fullTrack);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("album")]
        public ObjectResult UpdateAlbum(Album a)
        {
            return new ObjectResult("Not implemented") { StatusCode = 501 };
            //try
            //{
            //
            //    var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            //
            //    var mongoDatabase = mongoClient.GetDatabase("Melon");
            //
            //    var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            //
            //    var albumFilter = Builders<Album>.Filter.Eq("_id", a._id);
            //
            //    AlbumsCollection.ReplaceOne(albumFilter, a);
            //
            //    return new ObjectResult("Album updated") { StatusCode = 200 };
            //}
            //catch (Exception e)
            //{
            //    return new ObjectResult("Album Not Found") { StatusCode = 500 };
            //}
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public ObjectResult GetAlbums([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            List<Album> albums = new List<Album>();
            foreach (var id in ids)
            {
                var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);
                var album = AlbumCollection.Find(albumFilter).FirstOrDefault();
                if (album != null)
                {
                    album.Tracks = null;
                    albums.Add(album);
                }
            }


            return new ObjectResult(albums) { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist")]
        public ObjectResult GetArtist(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", id);

            var ArtistDocs = ArtistCollection.Find(artistFilter)
                                            .ToList();

            var artist = ArtistDocs.FirstOrDefault();
            if(artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
            artist.Releases = null;
            artist.SeenOn = null;
            artist.Tracks = null;

            return new ObjectResult(artist) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/tracks")]
        public ObjectResult GetArtistTracks(string id, uint page = 0, uint count = 50)
        {
            if(page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            List<Track> tracks = new List<Track>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Track>.Filter.Eq(x => x.TrackId, artist.Tracks[i].TrackId);
                    var fullTrack = TracksCollection.Find(filter).FirstOrDefault();
                    tracks.Add(fullTrack);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/releases")]
        public ObjectResult GetArtistReleases(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            List<Album> albums = new List<Album>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Album>.Filter.Eq(x => x.AlbumId, artist.Releases[i].AlbumId);
                    var fullAlbum = AlbumCollection.Find(filter).FirstOrDefault();
                    fullAlbum.Tracks = null;
                    albums.Add(fullAlbum);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/seen-on")]
        public ObjectResult GetArtistSeenOn(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
            List<Album> albums = new List<Album>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Album>.Filter.Eq(x => x.AlbumId, artist.SeenOn[i].AlbumId);
                    var fullAlbum = AlbumCollection.Find(filter).FirstOrDefault();
                    fullAlbum.Tracks = null;
                    albums.Add(fullAlbum);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("artist")]
        public ObjectResult UpdateArtist(Artist a)
        {
            return new ObjectResult("Not Implemented") { StatusCode = 501 };
            //try
            //{
            //    var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            //
            //    var mongoDatabase = mongoClient.GetDatabase("Melon");
            //
            //    var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            //
            //    var artistFilter = Builders<Artist>.Filter.Eq("_id", a._id);
            //
            //    ArtistCollection.ReplaceOne(artistFilter, a);
            //
            //    return new ObjectResult(") { StatusCode = 200 };
            //}
            //catch (Exception e)
            //{
            //    return new ObjectResult("Artist not found") { StatusCode = 500 };
            //}
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public ObjectResult GetArtists([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            List<Artist> artists = new List<Artist>();
            foreach (var id in ids)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", id);
                var artist = ArtistCollection.Find(artistFilter).FirstOrDefault();
                if (artist != null)
                {
                    artist.Releases = null;
                    artist.SeenOn = null;
                    artist.Tracks = null;
                    artists.Add(artist);
                }
            }


            return new ObjectResult(artists) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("lyrics")]
        public ObjectResult GetLyrics(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);
            var track = TracksCollection.Find(trackFilter).FirstOrDefault();

            if(track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            if(track.LyricsPath == "")
            {
                return new ObjectResult("Track Does Not Have Lyrics") { StatusCode = 404 };
            }

            string txt = System.IO.File.ReadAllText(track.LyricsPath);

            return new ObjectResult(txt) { StatusCode = 200 };
        }
    }
}
