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
using NuGet.Packaging.Signing;
using MongoDB.Bson.Serialization;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/discover")]
    public class DiscoverController : ControllerBase
    {
        private readonly ILogger<DiscoverController> _logger;

        public DiscoverController(ILogger<DiscoverController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User")]
        [HttpGet("tracks")]
        public ObjectResult DiscoverTracks([FromQuery] List<string> ids, bool orderByFavorites = false, bool orderByDiscovery = false, int count = 100, 
                                           bool enableTrackLinks = true, bool includeArtists = true, bool includeGenres = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            HashSet<string> Genres = new HashSet<string>();
            HashSet<string> Artists = new HashSet<string>();
            HashSet<string> NewTrackIds = new HashSet<string>();

            var filter = Builders<Track>.Filter.In(x => x._id, ids);
            var tracks = TracksCollection.Find(filter).ToList();
            foreach (var track in tracks)
            {
                foreach (var genre in track.TrackGenres) { Genres.Add(genre); }
                foreach (var artist in track.TrackArtists) { Artists.Add(artist._id); }
            }
            Genres.Remove("");
            Artists.Remove("");


            if (includeArtists)
            {
                var artistFilter = Builders<Artist>.Filter.In(x => x._id, Artists);
                var artists = ArtistsCollection.Find(artistFilter).ToList();

                HashSet<string> artistIds = new HashSet<string>();
                foreach (var artist in artists)
                {
                    foreach (var a in artist.ConnectedArtists.Select(x => x._id))
                    {
                        artistIds.Add(a);
                    }
                }

                var artistConnectionsFilter = Builders<Artist>.Filter.In(x => x._id, artistIds);
                var connections = ArtistsCollection.Find(artistConnectionsFilter).ToList();

                foreach (var artist in connections)
                {
                    if (!artists.Where(x => x._id == artist._id).Any())
                    {
                        artists.Add(artist);
                    }
                }

                foreach (var artist in artists)
                {
                    foreach (var track in artist.Tracks)
                    {
                        NewTrackIds.Add(track._id);
                    }
                }
            }
            if (includeGenres)
            {
                var projection = Builders<Track>.Projection.Include(x => x._id);
                var genrefilter = Builders<Track>.Filter.AnyIn(x => x.TrackGenres, Genres);
                var genreBasedTracks = TracksCollection.Find(genrefilter).Project(projection).ToList();

                foreach (var track in genreBasedTracks)
                {
                    NewTrackIds.Add(track["TrackId"].AsString);
                }
            }

            var finalFilter = Builders<Track>.Filter.In(x => x._id, NewTrackIds);
            var finalTracks = TracksCollection.Find(finalFilter).ToList();

            finalTracks = finalTracks.Where(x => ids.Contains(x._id) == false).ToList();

            string username = User.Identity.Name;
            if (orderByFavorites)
            {
                finalTracks = MelonAPI.ShuffleTracks(finalTracks, username, Melon.Types.ShuffleType.ByTrackFavorites, false, enableTrackLinks);
            }
            else if (orderByDiscovery)
            {
                finalTracks = MelonAPI.ShuffleTracks(finalTracks, username, Melon.Types.ShuffleType.ByTrackDiscovery, false, enableTrackLinks);
            }
            else
            {
                finalTracks = MelonAPI.ShuffleTracks(finalTracks, username, Melon.Types.ShuffleType.ByTrack, false, enableTrackLinks);
            }

            count = count <= finalTracks.Count ? count : finalTracks.Count;
            return new ObjectResult(finalTracks.Slice(0,count).Select(x=>new DbLink(x))) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("albums")]
        public ObjectResult DiscoverAlbums([FromQuery] List<string> ids, bool shuffle = true, int count = 100, int page = 0, bool includeArtists = true, bool includeGenres = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            HashSet<string> Genres = new HashSet<string>();
            HashSet<string> Artists = new HashSet<string>();
            HashSet<string> NewAlbumIds = new HashSet<string>();

            var filter = Builders<Album>.Filter.In(x => x._id, ids);
            var albums = AlbumsCollection.Find(filter).ToList();
            foreach (var track in albums)
            {
                foreach (var genre in track.AlbumGenres) { Genres.Add(genre); }
                foreach (var artist in track.AlbumArtists) { Artists.Add(artist._id); }
                foreach (var artist in track.ContributingArtists) { Artists.Add(artist._id); }
            }
            Genres.Remove("");
            Artists.Remove("");

            List<Album> finalAlbums = new List<Album>();
            if (includeArtists)
            {
                var artistFilter = Builders<Artist>.Filter.In(x => x._id, Artists);
                var artists = ArtistsCollection.Find(artistFilter).ToList();

                HashSet<string> artistIds = new HashSet<string>();
                foreach (var artist in artists)
                {
                    foreach (var a in artist.ConnectedArtists.Select(x => x._id))
                    {
                        artistIds.Add(a);
                    }
                }

                foreach (var artist in artists)
                {
                    foreach (var album in artist.Releases)
                    {
                        NewAlbumIds.Add(album._id);
                    }
                    foreach (var album in artist.SeenOn)
                    {
                        NewAlbumIds.Add(album._id);
                    }
                }

                var final = Builders<Album>.Filter.In(x => x._id, NewAlbumIds);
                finalAlbums = AlbumsCollection.Find(final).ToList();
                NewAlbumIds.Clear();

                var artistConnectionsFilter = Builders<Artist>.Filter.In(x => x._id, artistIds);
                var connections = ArtistsCollection.Find(artistConnectionsFilter).ToList();

                foreach (var artist in connections)
                {
                    if (!artists.Where(x => x._id == artist._id).Any())
                    {
                        foreach (var album in artist.Releases)
                        {
                            NewAlbumIds.Add(album._id);
                        }
                        foreach (var album in artist.SeenOn)
                        {
                            NewAlbumIds.Add(album._id);
                        }
                    }
                }
            }

            if (includeGenres)
            {
                var projection = Builders<Album>.Projection.Include(x => x._id);
                var genrefilter = Builders<Album>.Filter.AnyIn(x => x.AlbumGenres, Genres);
                var genreBasedAlbums = AlbumsCollection.Find(genrefilter).Project(projection).ToList();

                foreach (var album in genreBasedAlbums)
                {
                    NewAlbumIds.Add(album["AlbumId"].AsString);
                }
            }

            var finalFilter = Builders<Album>.Filter.In(x => x._id, NewAlbumIds);
            var exAlbums = AlbumsCollection.Find(finalFilter).ToList();

            finalAlbums.AddRange(exAlbums.Where(x => ids.Contains(x._id) == false).ToList());

            string username = User.Identity.Name;
            if (shuffle)
            {
                Random rng = new Random();
                int n = finalAlbums.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    Album value = finalAlbums[k];
                    finalAlbums[k] = finalAlbums[n];
                    finalAlbums[n] = value;
                }

                // Remove any consecutive tracks from the same artist or album.
                for (int l = 0; l < 5; l++)
                {
                    for (int i = 0; i < finalAlbums.Count() - 1; i++)
                    {
                        if (finalAlbums[i].AlbumArtists.Contains(finalAlbums[i + 1].AlbumArtists[0]) || finalAlbums[i].ContributingArtists == finalAlbums[i + 1].ContributingArtists)
                        {
                            var temp = finalAlbums[i];
                            finalAlbums.RemoveAt(i);
                            var k = rng.Next(0, finalAlbums.Count());
                            finalAlbums.Insert(k, temp);
                        }
                    }
                }
            }

            count = count <= finalAlbums.Count ? count : finalAlbums.Count;
            var end = (count * page) + count <= finalAlbums.Count ? (count * page) + count : finalAlbums.Count;
            return new ObjectResult(finalAlbums.Take(new Range(count*page, end)).Select(x=>new DbLink(x))) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("artists")]
        public ObjectResult DiscoverArtists([FromQuery] List<string> ids, int count = 100, int page = 0, bool shuffle = true)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            HashSet<string> Genres = new HashSet<string>();

            var filter = Builders<Artist>.Filter.In(x => x._id, ids);
            var artists = ArtistsCollection.Find(filter).ToList();
            foreach (var artist in artists)
            {
                foreach (var genre in artist.Genres) { Genres.Add(genre); }
            }
            Genres.Remove("");


            var genrefilter = Builders<Artist>.Filter.AnyIn(x => x.Genres, Genres);
            var genreBasedArtists = ArtistsCollection.Find(genrefilter).ToList();


            genreBasedArtists = genreBasedArtists.Where(x => ids.Contains(x._id) == false).ToList();

            string username = User.Identity.Name;
            if (shuffle)
            {
                Random rng = new Random();
                int n = genreBasedArtists.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    Artist value = genreBasedArtists[k];
                    genreBasedArtists[k] = genreBasedArtists[n];
                    genreBasedArtists[n] = value;
                }
            }

            count = count <= genreBasedArtists.Count ? count : genreBasedArtists.Count;
            var end = (count * page) + count <= genreBasedArtists.Count ? (count * page) + count : genreBasedArtists.Count;
            return new ObjectResult(genreBasedArtists.Take(new Range(count * page, end)).Select(x => new DbLink(x))) { StatusCode = 200 };
        }
    }
}
