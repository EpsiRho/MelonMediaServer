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
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);

                var trackDocs = TracksCollection.Find(trackFilter)
                                                .ToList();


                return new ObjectResult(trackDocs[0]) { StatusCode = 200 };
            }
            catch (Exception)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("track")]
        public ObjectResult UpdateTrack(Track t)
        {
            return new ObjectResult("Not Implemented") { StatusCode = 501 };
            //try
            //{
            //    var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            //
            //    var mongoDatabase = mongoClient.GetDatabase("Melon");
            //
            //    var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            //
            //    var trackFilter = Builders<Track>.Filter.Eq("_id", t._id);
            //
            //    TracksCollection.ReplaceOne(trackFilter, t);
            //
            //    return new ObjectResult("Track updated") { StatusCode = 200 };
            //}
            //catch (Exception)
            //{
            //    return new ObjectResult("Track not found") { StatusCode = 404 };
            //}
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult GetTracks([FromQuery] string[] ids)
        {
            try
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
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }
        }

        // Albums
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album")]
        public ObjectResult GetAlbum(string id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

                var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);

                var albumDocs = AlbumsCollection.Find(albumFilter)
                                                .ToList();

                var album = albumDocs[0];
                album.Tracks = null;

                return new ObjectResult(album) { StatusCode = 200 };
            }
            catch (Exception e)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album/tracks")]
        public ObjectResult GetAlbumTracks(string id, int page = 0, int count = 50)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
                var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);

                var albumDocs = AlbumsCollection.Find(albumFilter)
                                                .ToList();

                var album = albumDocs[0];
                List<Track> tracks = new List<Track>();
                for(int i = (page * count); i < ((page * count) + count); i++)
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
            catch (Exception e)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }
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
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

                List<Album> albums = new List<Album>();
                foreach (var id in ids)
                {
                    var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);
                    var album = AlbumCollection.Find(albumFilter).FirstOrDefault();
                    albums.Add(album);
                }


                return new ObjectResult(albums) { StatusCode = 200 };
            }
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }
        }

        // Artists
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist")]
        public ObjectResult GetArtist(string id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

                var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", id);

                var ArtistDocs = ArtistCollection.Find(artistFilter)
                                                .ToList();

                var artist = ArtistDocs.FirstOrDefault();
                artist.Releases = null;
                artist.SeenOn = null;
                artist.Tracks = null;

                return new ObjectResult(artist) { StatusCode = 200 };
            }
            catch (Exception e)
            {
                return new ObjectResult("Artist not found") { StatusCode = 500 };
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/tracks")]
        public ObjectResult GetArtistTracks(string id, int page = 0, int count = 50)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
                var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                var artistFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);

                var artistDocs = ArtistsCollection.Find(artistFilter)
                                                .ToList();

                var artist = artistDocs[0];
                List<Track> tracks = new List<Track>();
                for (int i = (page * count); i < ((page * count) + count); i++)
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
            catch (Exception e)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/releases")]
        public ObjectResult GetArtistReleases(string id, int page = 0, int count = 50)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
                var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

                var artistFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);

                var artistDocs = ArtistsCollection.Find(artistFilter)
                                                .ToList();

                var artist = artistDocs[0];
                List<Album> albums = new List<Album>();
                for (int i = (page * count); i < ((page * count) + count); i++)
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
            catch (Exception e)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/seen-on")]
        public ObjectResult GetArtistSeenOn(string id, int page = 0, int count = 50)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
                var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

                var artistFilter = Builders<Artist>.Filter.Eq(x=>x.ArtistId, id);

                var artistDocs = ArtistsCollection.Find(artistFilter)
                                                .ToList();

                var artist = artistDocs[0];
                List<Album> albums = new List<Album>();
                for (int i = (page * count); i < ((page * count) + count); i++)
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
            catch (Exception e)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
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
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

                List<Artist> artists = new List<Artist>();
                foreach (var id in ids)
                {
                    var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", id);
                    var artist = ArtistCollection.Find(artistFilter).FirstOrDefault();
                    artists.Add(artist);
                }


                return new ObjectResult(artists) { StatusCode = 200 };
            }
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("lyrics")]
        public ObjectResult GetLyrics(string id)
        {
            try
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
            catch (Exception e)
            {
                return new ObjectResult(e.Message) { StatusCode = 500 };
            }
        }
    }
}
