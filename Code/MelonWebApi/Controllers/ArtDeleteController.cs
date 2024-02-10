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
using System.Web.Http.Filters;
using MongoDB.Bson.Serialization;
using System.Security.Claims;
using System.Net;
using ATL.Playlist;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/art/delete")]
    public class ArtDeleteController : ControllerBase
    {
        private readonly ILogger<ArtDeleteController> _logger;

        public ArtDeleteController(ILogger<ArtDeleteController> logger)
        {
            _logger = logger;
        }

        // Tracks
        [Authorize(Roles = "Admin")]
        [HttpPost("track-art")]
        public ObjectResult DeleteTrackArt(string id, int pos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var track = TracksCollection.Find(Builders<Track>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if(track == null)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            if(track.TrackArtCount < pos)
            {
                return new ObjectResult("Invalid position") { StatusCode = 400 };
            }

            ATL.Track file = null;
            try
            {
                file = new ATL.Track(track.Path);
            }
            catch (Exception)
            {
                return new ObjectResult("Track file not found") { StatusCode = 404 };
            }

            file.EmbeddedPictures.RemoveAt(pos);
            file.Save();

            track.TrackArtCount--;
            TracksCollection.ReplaceOne(Builders<Track>.Filter.Eq(x => x._id, id), track);

            return new ObjectResult("Track art removed") { StatusCode = 200 };
        }

        // Albums
        [Authorize(Roles = "Admin")]
        [HttpPost("album-art")]
        public ObjectResult DeleteAlbumArt(string id, int pos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");

            var album = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }

            if (album.AlbumArtCount < pos)
            {
                return new ObjectResult("Invalid position") { StatusCode = 400 };
            }

            var filePath = $"{StateManager.melonPath}/AlbumArts/{album._id}-{pos}.jpg";

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            album.AlbumArtPaths.Remove($"{album._id}-{pos}.jpg");
            album.AlbumArtCount--;
            AlbumsCollection.ReplaceOne(Builders<Album>.Filter.Eq(x => x._id, id), album);

            return new ObjectResult("Album art removed") { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-pfp")]
        public ObjectResult DeleteArtistPfp(string id, int pos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            if (artist.ArtistPfpArtCount < pos)
            {
                return new ObjectResult("Invalid position") { StatusCode = 400 };
            }

            var filePath = $"{StateManager.melonPath}/ArtistPfps/{artist._id}-{pos}.jpg";

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            artist.ArtistPfpPaths.Remove($"{artist._id}-{pos}.jpg");
            artist.ArtistPfpArtCount--;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            return new ObjectResult("Artist pfp removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-banner")]
        public ObjectResult DeleteArtistBanner(string id, int pos)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            if (artist.ArtistPfpArtCount < pos)
            {
                return new ObjectResult("Invalid position") { StatusCode = 400 };
            }

            var filePath = $"{StateManager.melonPath}/ArtistBanners/{artist._id}-{pos}.jpg";

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            artist.ArtistBannerPaths.Remove($"{artist._id}-{pos}.jpg");
            artist.ArtistBannerArtCount--;
            ArtistsCollection.ReplaceOne(Builders<Artist>.Filter.Eq(x => x._id, id), artist);

            return new ObjectResult("Artist banner removed") { StatusCode = 200 };
        }

        // Playlists
        [Authorize(Roles = "Admin")]
        [HttpPost("playlist-art")]
        public ObjectResult DeletePlaylistArt(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var PlaylistsCollection = mongoDatabase.GetCollection<Playlist>("Playlists");

            var playlist = PlaylistsCollection.Find(Builders<Playlist>.Filter.Eq(x => x._id, id)).FirstOrDefault();
            if (playlist == null)
            {
                return new ObjectResult("Playlist not found") { StatusCode = 404 };
            }

            var filePath = $"{StateManager.melonPath}/PlaylistArts/{playlist._id}.jpg";

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception)
            {
                return new ObjectResult("File error") { StatusCode = 404 };
            }

            playlist.ArtworkPath = "";
            PlaylistsCollection.ReplaceOne(Builders<Playlist>.Filter.Eq(x => x._id, id), playlist);

            return new ObjectResult("Playlist art removed") { StatusCode = 200 };
        }

    }
}
