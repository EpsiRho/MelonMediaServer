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
        [HttpGet("track")]
        public Track GetTrack(string _id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(_id));

                var trackDocs = TracksCollection.Find(trackFilter)
                                                .ToList();

                return trackDocs[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
        [HttpPatch("track")]
        public string UpdateTrack(Track t)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                var trackFilter = Builders<Track>.Filter.Eq("_id", t._id);

                TracksCollection.ReplaceOne(trackFilter, t);

                return "200";
            }
            catch (Exception)
            {
                return "404";
            }
        }

        // Albums
        [HttpGet("album")]
        public Album GetAlbum(string _id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

                var albumFilter = Builders<Album>.Filter.Eq("_id", new ObjectId(_id));

                var albumDocs = AlbumsCollection.Find(albumFilter)
                                                .ToList();

                return albumDocs[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
        [HttpPatch("album")]
        public string UpdateAlbum(Album a)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

                var albumFilter = Builders<Album>.Filter.Eq("_id", a._id);

                AlbumsCollection.ReplaceOne(albumFilter, a);

                return "200";
            }
            catch (Exception)
            {
                return "404";
            }
        }

        // Artists
        [HttpGet("artist")]
        public Artist GetArtist(string _id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

                var artistFilter = Builders<Artist>.Filter.Eq("_id", new ObjectId(_id));

                var ArtistDocs = ArtistCollection.Find(artistFilter)
                                                .ToList();

                return ArtistDocs[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
        [HttpPatch("artist")]
        public string UpdateArtist(Artist a)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

                var artistFilter = Builders<Artist>.Filter.Eq("_id", a._id);

                ArtistCollection.ReplaceOne(artistFilter, a);

                return "200";
            }
            catch (Exception)
            {
                return "404";
            }
        }
    }
}
