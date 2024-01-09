using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using DnsClient;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/db")]
    public class DatabaseController : ControllerBase
    {
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(ILogger<DatabaseController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("format")]
        public ObjectResult GetFormat()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
                
            var TracksFilter = Builders<Track>.Filter.Empty;

            List<Track> Tracks = new List<Track>();
            HashSet<string> formats = new HashSet<string>();
            int page = 0;
            do
            {
                Tracks = TracksCollection.Find(TracksFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from track in Tracks
                             select track.Format).Distinct();
                foreach(var item in items)
                {
                    formats.Add(item);
                }
                page++;
            } while (Tracks.Count() == 1000);


            return new ObjectResult(formats.OrderBy(x=>x)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("bitrate")]
        public ObjectResult GetBitrate()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var TracksFilter = Builders<Track>.Filter.Empty;

            List<Track> Tracks = new List<Track>();
            HashSet<string> bitrates = new HashSet<string>();
            int page = 0;
            do
            {
                Tracks = TracksCollection.Find(TracksFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from track in Tracks
                             select track.Bitrate).Distinct();
                foreach (var item in items)
                {
                    bitrates.Add(item);
                }
                page++;
            } while (Tracks.Count() == 1000);


            return new ObjectResult(bitrates.OrderBy(x => Convert.ToInt32(x))) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("sample-rate")]
        public ObjectResult GetSampleRate()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var TracksFilter = Builders<Track>.Filter.Empty;

            List<Track> Tracks = new List<Track>();
            HashSet<string> sampleRates = new HashSet<string>();
            int page = 0;
            do
            {
                Tracks = TracksCollection.Find(TracksFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from track in Tracks
                             select track.SampleRate).Distinct().ToList();
                foreach (var item in items)
                {
                    sampleRates.Add(item);
                }
                page++;
            } while (Tracks.Count() == 1000);


            return new ObjectResult(sampleRates.OrderBy(x => Convert.ToInt32(x))) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("bits-per-sample")]
        public ObjectResult GetBitsPerSample()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var TracksFilter = Builders<Track>.Filter.Empty;

            List<Track> Tracks = new List<Track>();
            HashSet<string> bitsPerSample = new HashSet<string>();
            int page = 0;
            do
            {
                Tracks = TracksCollection.Find(TracksFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from track in Tracks
                             select track.BitsPerSample).Distinct().ToList();
                foreach (var item in items)
                {
                    bitsPerSample.Add(item);
                }
                page++;
            } while (Tracks.Count() == 1000);


            return new ObjectResult(bitsPerSample.OrderBy(x => x)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("channel")]
        public ObjectResult GetChannel()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var TracksFilter = Builders<Track>.Filter.Empty;

            List<Track> Tracks = new List<Track>();
            HashSet<string> channels = new HashSet<string>();
            int page = 0;
            do
            {
                Tracks = TracksCollection.Find(TracksFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from track in Tracks
                             select track.Channels).Distinct().ToList();
                foreach (var item in items)
                {
                    channels.Add(item);
                }
                page++;
            } while (Tracks.Count() == 1000);


            return new ObjectResult(channels.OrderBy(x => x)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("release-status")]
        public ObjectResult GetReleaseStatus()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var AlbumFilter = Builders<Album>.Filter.Empty;

            List<Album> Albums = new List<Album>();
            HashSet<string> rleaseStatuses = new HashSet<string>();
            int page = 0;
            do
            {
                Albums = AlbumCollection.Find(AlbumFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from album in Albums
                             select album.ReleaseStatus).Distinct().ToList();
                foreach (var item in items)
                {
                    rleaseStatuses.Add(item);
                }
                page++;
            } while (Albums.Count() == 1000);


            return new ObjectResult(rleaseStatuses.OrderBy(x => x)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("release-type")]
        public ObjectResult GetReleaseType()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var AlbumFilter = Builders<Album>.Filter.Empty;

            List<Album> Albums = new List<Album>();
            HashSet<string> releaseTypes = new HashSet<string>();
            int page = 0;
            do
            {
                Albums = AlbumCollection.Find(AlbumFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from album in Albums
                             select album.ReleaseType).Distinct().ToList();
                foreach (var item in items)
                {
                    releaseTypes.Add(item);
                }
                page++;
            } while (Albums.Count() == 1000);


            return new ObjectResult(releaseTypes.OrderBy(x => x)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("publisher")]
        public ObjectResult GetPublisher()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var AlbumFilter = Builders<Album>.Filter.Empty;

            List<Album> Albums = new List<Album>();
            HashSet<string> publishers = new HashSet<string>();
            int page = 0;
            do
            {
                Albums = AlbumCollection.Find(AlbumFilter)
                                         .Skip(page * 1000)
                                         .Limit(1000)
                                         .ToList();
                var items = (from album in Albums
                             select album.Publisher).Distinct().ToList();
                foreach (var item in items)
                {
                    publishers.Add(item);
                }
                page++;
            } while (Albums.Count() == 1000);


            return new ObjectResult(publishers.OrderBy(x => x)) { StatusCode = 200 };
        }

    }
}
