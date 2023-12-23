﻿using Melon.Models;
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
        public Track GetTrack(string id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

                var trackFilter = Builders<Track>.Filter.Eq("_id", new ObjectId(id));

                var trackDocs = TracksCollection.Find(trackFilter)
                                                .ToList();

                return trackDocs[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
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
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public List<Track> GetTracks(string[] ids)
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


                return tracks;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Albums
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album")]
        public Album GetAlbum(string id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

                var albumFilter = Builders<Album>.Filter.Eq("_id", new ObjectId(id));

                var albumDocs = AlbumsCollection.Find(albumFilter)
                                                .ToList();

                return albumDocs[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
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
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public List<Album> GetAlbums(string[] ids)
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


                return albums;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Artists
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist")]
        public Artist GetArtist(string id)
        {
            try
            {
                var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

                var mongoDatabase = mongoClient.GetDatabase("Melon");

                var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

                var artistFilter = Builders<Artist>.Filter.Eq("_id", new ObjectId(id));

                var ArtistDocs = ArtistCollection.Find(artistFilter)
                                                .ToList();

                return ArtistDocs[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
        [Authorize(Roles = "Admin,User,Pass")]
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
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public List<Artist> GetArtists(string[] ids)
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


                return artists;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
