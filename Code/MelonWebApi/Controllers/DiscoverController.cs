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
using NuGet.Packaging;
using System.Collections.Generic;
using System.Security.Claims;

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
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/discover/tracks", curId, new Dictionary<string, object>()
            {
                { "ids", ids },
                { "orderByFavorites", orderByFavorites },
                { "orderByDiscovery", orderByDiscovery },
                { "count", count },
                { "enableTrackLinks", enableTrackLinks },
                { "includeArtists", includeArtists },
                { "includeGenres", includeGenres }
            });

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
                    NewTrackIds.Add(track["_id"].AsString);
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

            args.SendEvent("Sent a List of DbLinks", 200, Program.mWebApi);
            return new ObjectResult(finalTracks.Slice(0,count).Select(x=>new DbLink(x))) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("albums")]
        public ObjectResult DiscoverAlbums([FromQuery] List<string> ids, bool shuffle = true, int count = 100, int page = 0, 
                                           bool includeArtists = true, bool includeGenres = true)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/discover/albums", curId, new Dictionary<string, object>()
            {
                { "ids", ids },
                { "shuffle", shuffle },
                { "page", page },
                { "count", count },
                { "includeArtists", includeArtists },
                { "includeGenres", includeGenres }
            });

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
                    NewAlbumIds.Add(album["_id"].AsString);
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
            args.SendEvent("Sent a List of DbLinks", 200, Program.mWebApi);
            return new ObjectResult(finalAlbums.Take(new Range(count*page, end)).Select(x=>new DbLink(x))) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("artists")]
        public ObjectResult DiscoverArtists([FromQuery] List<string> ids, int count = 100, int page = 0, bool shuffle = true, 
                                            bool includeConnections = true, bool includeGenres = true)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/discover/artists", curId, new Dictionary<string, object>()
            {
                { "ids", ids },
                { "shuffle", shuffle },
                { "page", page },
                { "count", count },
                { "includeConnections", includeConnections },
                { "includeGenres", includeGenres }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            HashSet<string> Genres = new HashSet<string>();
            HashSet<DbLink> Artists = new HashSet<DbLink>();

            var filter = Builders<Artist>.Filter.In(x => x._id, ids);
            var artists = ArtistsCollection.Find(filter).ToList();
            foreach (var artist in artists)
            {
                foreach (var genre in artist.Genres) { Genres.Add(genre); }
                foreach (var conn in artist.ConnectedArtists) { Artists.Add(conn); }
            }
            Genres.Remove("");

            Random rng = new Random();
            Dictionary<DbLink, double> finalArtists = new Dictionary<DbLink, double>();
            if (includeConnections)
            {
                finalArtists.AddRange(Artists.Select(x=>KeyValuePair.Create(x,0.2)));
            }

            if (includeGenres)
            {
                var genrefilter = Builders<Artist>.Filter.AnyIn(x => x.Genres, Genres);
                var genreBasedArtists = ArtistsCollection.Find(genrefilter).ToList();

                genreBasedArtists = genreBasedArtists.Where(x => ids.Contains(x._id) == false).ToList();
                finalArtists.AddRange(genreBasedArtists.Select(x=> KeyValuePair.Create(new DbLink(x), 0.0)));
            }

            string username = User.Identity.Name;
            count = count <= finalArtists.Count ? count : finalArtists.Count;
            var end = (count * page) + count <= finalArtists.Count ? (count * page) + count : finalArtists.Count;
            if (shuffle)
            {
                var shuffledArtists = finalArtists.OrderByDescending(item => item.Value + rng.NextDouble())
                                           .Select(item => item)
                                           .ToList();

                return new ObjectResult(shuffledArtists.Take(new Range(count * page, end)).Select(x=>x.Key)) { StatusCode = 200 };
            }

            args.SendEvent("Sent a List of DbLinks", 200, Program.mWebApi);
            return new ObjectResult(finalArtists.Take(new Range(count * page, end)).Select(x => x.Key)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("time")]
        public ObjectResult DiscoverTimeBasedTracks(string time, int span = 5, int count = 100, bool enableTrackLinks = true, bool includeArtists = true, bool includeGenres = true,
                                                    bool includeRecent = true)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/discover/artists", curId, new Dictionary<string, object>()
            {
                { "time", time },
                { "span", span },
                { "count", count },
                { "enableTrackLinks", enableTrackLinks },
                { "includeArtists", includeArtists },
                { "includeGenres", includeGenres },
                { "includeRecent", includeRecent }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            HashSet<string> Genres = new HashSet<string>();
            HashSet<string> Artists = new HashSet<string>();
            HashSet<string> TrackIds = new HashSet<string>();
            HashSet<string> NewTrackIds = new HashSet<string>();

            DateTime ts = DateTime.Parse(time);
            DateTime tsStart = ts.Subtract(TimeSpan.FromMinutes(30));
            DateTime tsEnd = ts.Add(TimeSpan.FromMinutes(30));

            var stats = StatsCollection.AsQueryable()
                                       .Where(x => x.LogDate >= ts.AddDays(-span) &&
                                              x.LogDate.TimeOfDay >= tsStart.TimeOfDay && x.LogDate.TimeOfDay < tsEnd.TimeOfDay)
                                       .ToList();
            foreach (var stat in stats)
            {
                foreach (var genre in stat.Genres) { Genres.Add(genre); }
                foreach (var artist in stat.ArtistIds) { Artists.Add(artist); }
                TrackIds.Add(stat.TrackId);
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
                    NewTrackIds.Add(track["_id"].AsString);
                }
            }

            var firstFilter = Builders<Track>.Filter.In(x => x._id, TrackIds);
            var firstTracks = TracksCollection.Find(firstFilter).ToList();

            var finalFilter = Builders<Track>.Filter.In(x => x._id, NewTrackIds);
            var finalTracks = TracksCollection.Find(finalFilter).ToList();

            string username = User.Identity.Name;

            Dictionary<Track, double> tracks = finalTracks.Select(x => KeyValuePair.Create(x, 0.2)).ToDictionary();
            if (includeRecent)
            {
                tracks.AddRange(firstTracks.Select(x => KeyValuePair.Create(x, 0.0)).ToDictionary());
            }

            Random rng = new Random();
            List<Track> shuffledList = tracks.OrderBy(item => rng.NextDouble() + item.Value)
                                             .Select(item => item.Key)
                                             .ToList();

            if (enableTrackLinks)
            {
                for (int i = 0; i < shuffledList.Count() - 1; i++)
                {
                    if (shuffledList[i].nextTrack != "")
                    {
                        for (int j = 0; j < shuffledList.Count(); j++)
                        {
                            if (shuffledList[j]._id == shuffledList[i].nextTrack)
                            {
                                var temp = shuffledList[i + 1];
                                shuffledList[i + 1] = shuffledList[j];
                                shuffledList[j] = temp;
                                break;
                            }
                        }
                    }
                }
            }

            count = count <= shuffledList.Count ? count : shuffledList.Count;
            args.SendEvent("Sent a List of DbLinks", 200, Program.mWebApi);
            return new ObjectResult(shuffledList.Slice(0, count).Select(x => new DbLink(x))) { StatusCode = 200 };
        }
    }
}
