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

                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));

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
                    var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));
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

                var albumFilter = Builders<Album>.Filter.Eq("_id", new ObjectId(id));

                var albumDocs = AlbumsCollection.Find(albumFilter)
                                                .ToList();

                return new ObjectResult(albumDocs[0]) { StatusCode = 200 };
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
        public ObjectResult GetAlbums(string[] ids)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

                List<Album> albums = new List<Album>();
                foreach (var id in ids)
                {
                    var albumFilter = Builders<Album>.Filter.Eq("_id", new ObjectId(id));
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

                var artistFilter = Builders<Artist>.Filter.Eq("_id", new ObjectId(id));

                var ArtistDocs = ArtistCollection.Find(artistFilter)
                                                .ToList();

                return new ObjectResult(ArtistDocs[0]) { StatusCode = 200 };
            }
            catch (Exception e)
            {
                return new ObjectResult("Artist not found") { StatusCode = 500 };
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
        public ObjectResult GetArtists(string[] ids)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

                List<Artist> artists = new List<Artist>();
                foreach (var id in ids)
                {
                    var artistFilter = Builders<Artist>.Filter.Eq("_id", new ObjectId(id));
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
    }
}
