using Melon.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MongoDB.Bson;
using MelonLib.API;
using NAudio.CoreAudioApi;
using System;
using MongoDB.Bson.Serialization;


namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("")]
    public class UserPageController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        private readonly IWebHostEnvironment _environment;

        public UserPageController(ILogger<AuthController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Login with a username and password to get a JWT for authenticating future api calls.
        /// </summary>
        /// <param name="username">The username to authenticate with.</param>
        /// <param name="password">The password to authenticate with.</param>
        /// <returns>Returns a JWT token on success.</returns>
        /// <response code="200">The username and password match.</response>
        /// <response code="401">If the username isn't found or the password doesn't match.</response>
        [HttpGet("user-page")]
        public IActionResult UserPage(string id)
        {
            var args = new WebApiEventArgs("user-page", "No Auth", new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var user = UserCollection.Find(userFilter).FirstOrDefault();

            if (user == null)
            {
                return Content("User Not Found", "text/html");
            }

            string vars = "";

            // User Profile
            byte[] userpfp = null;
            try
            {
                FileStream file = new FileStream($"{StateManager.melonPath}/UserPfps/{user._id}.png", FileMode.Open, FileAccess.Read);
                userpfp = new byte[file.Length];
                file.Read(userpfp, 0, (int)file.Length);
                file.Dispose();
            }
            catch (Exception)
            {
                userpfp = StateManager.GetDefaultImage();
            }
            string userpfpBase64 = Convert.ToBase64String(userpfp);
            vars += $"const userInfo = {{\n\tusername: \"{user.Username}\",\n\tprofilePicture: \"data:image/jpeg;base64,{userpfpBase64}\",\n\tbio: \"{user.Bio}\",\n [FavoriteTracksHere] }};\n";


            // Add Favorites
            var FavTrack = TracksCollection.Find(Builders<Track>.Filter.Eq(x => x._id, user.FavTrack)).FirstOrDefault();
            var FavAlbum = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, user.FavAlbum)).FirstOrDefault();
            var FavArtist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, user.FavArtist)).FirstOrDefault();

            string favs = ""; 
            if (FavTrack != null)
            {
                favs = $"\"favoriteTrack\": {{ \n";
                string artwork = $"/api/download/track-art?id={FavTrack._id}";
                favs += $"name: \"{FavTrack.Name}\", artist: \"{String.Join(", ", FavTrack.TrackArtists.Select(x => x.Name).ToArray())}\", artwork: \"{artwork}\"}},\n";
            }
            else
            {
                favs = $"\"favoriteTrack\": {{ \n";
                string artwork = $"/defaultArtwork.png";
                favs += $"name: \"Not Set\", artist: \"N/A\", artwork: \"{artwork}\"}},\n";
            }

            if (FavAlbum != null)
            {
                favs += $"\"favoriteAlbum\": {{ \n";
                string artwork = $"/api/download/album-art?id={FavAlbum._id}";
                favs += $"name: \"{FavAlbum.Name}\", artist: \"{String.Join(", ", FavAlbum.AlbumArtists.Select(x => x.Name).ToArray())}\", artwork: \"{artwork}\"}},\n";
            }
            else
            {
                favs += $"\"favoriteAlbum\": {{ \n";
                string artwork = $"/defaultArtwork.png";
                favs += $"name: \"Not Set\", artist: \"N/A\", artwork: \"{artwork}\"}},\n";
            }

            if (FavArtist != null)
            {
                favs += $"\"favoriteArtist\": {{ \n";
                string artwork = $"/api/download/artist-pfp?id={FavArtist._id}";
                favs += $"name: \"{FavArtist.Name}\", artwork: \"{artwork}\"}},\n";
            }
            else
            {
                favs += $"\"favoriteArtist\": {{ \n";
                string artwork = $"/defaultArtwork.png";
                favs += $"name: \"Not Set\", artwork: \"{artwork}\"}},\n";
            }

            vars = vars.Replace("[FavoriteTracksHere]", favs);

            
            

            vars += $"const userId = '{id}';";

            var htmlContent = System.IO.File.ReadAllText($"{_environment.WebRootPath}/index.html");



            htmlContent = htmlContent.Replace("InputVarsHere", vars);

            return Content(htmlContent, "text/html");
        }
        [HttpGet("user-page/top-tracks")]
        public IActionResult UserPageTopTracks(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-tracks", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);

            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "month")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(30));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "year")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(365));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }


            var stats = StatsCollection.Find(statFilter).ToList();

            var tracks = stats.GroupBy(stat => stat.TrackId)
                              .Select(group => new { Name = group.Key, Count = group.Count() })
                              .OrderByDescending(x => x.Count)
                              .Take(new Range(page * count, (page * count) + count))
                              .ToDictionary(g => g.Name, g => g.Count);

            var trackIds = tracks.Keys.ToList();  // Extract the keys (which are _ids) from the dictionary
            var tracksFull = TracksCollection.Find(x => trackIds.Contains(x._id)).ToList();


            string obj = "[";
            foreach(var track in tracks)
            {
                var fullTrack = tracksFull.FirstOrDefault(x => x._id == track.Key);
                string artists = "";
                foreach (var a in fullTrack.TrackArtists)
                {
                    artists += $"{a.Name},";
                }
                artists = artists.Substring(0, artists.Length - 1);

                string artwork = $"/api/download/track-art?id={fullTrack._id}";
                obj += $"{{ \"name\": \"{fullTrack.Name.Replace("\"", "\\\"")}\", \"artist\": \"{artists}\", \"artwork\": \"{artwork}\", \"value\": \"{track.Value}\"}},";
            }
            obj = obj.Substring(0, obj.Length - 1);
            obj += "]";

            args.SendEvent("Top tracks sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }
        [HttpGet("user-page/top-albums")]
        public IActionResult UserPageTopAlbums(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-albums", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);


            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "month")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(30));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "year")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(365));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var albums = stats.GroupBy(stat => stat.AlbumId)
                              .Select(group => new { Name = group.Key, Count = group.Count() })
                              .OrderByDescending(x => x.Count)
                              .Take(new Range(page * count, (page * count) + count))
                              .ToDictionary(g => g.Name, g => g.Count);

            var albumIds = albums.Keys.ToList();  // Extract the keys (which are _ids) from the dictionary
            var albumsFull = AlbumsCollection.Find(x => albumIds.Contains(x._id)).ToList();


            string obj = "[";
            foreach (var album in albums)
            {
                var fullAlbum = albumsFull.FirstOrDefault(x => x._id == album.Key);
                string artists = "";
                foreach (var a in fullAlbum.AlbumArtists)
                {
                    artists += $"{a.Name},";
                }
                artists = artists.Substring(0, artists.Length - 1);

                string artwork = $"/api/download/album-art?id={fullAlbum._id}";
                obj += $"{{ \"name\": \"{fullAlbum.Name.Replace("\"", "\\\"")}\", \"artist\": \"{artists}\", \"artwork\": \"{artwork}\", \"value\": \"{album.Value}\"}},";
            }
            obj = obj.Substring(0, obj.Length - 1);
            obj += "]";

            args.SendEvent("Top albums sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }
        [HttpGet("user-page/top-artists")]
        public IActionResult UserPageTopArtists(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-artists", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);


            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "month")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(30));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "year")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(365));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var artists = stats.SelectMany(obj => obj.ArtistIds)
                               .GroupBy(x => x)
                               .Select(group => new { Name = group.Key, Count = group.Count() })
                               .OrderByDescending(x => x.Count)
                               .Take(new Range(page * count, (page * count) + count))
                               .ToDictionary(g => g.Name, g => g.Count);

            var artistsIds = artists.Keys.ToList();  // Extract the keys (which are _ids) from the dictionary
            var artistsFull = ArtistsCollection.Find(x => artistsIds.Contains(x._id)).ToList();


            string obj = "[";
            foreach (var album in artists)
            {
                var fullArtist = artistsFull.FirstOrDefault(x => x._id == album.Key);

                string artwork = $"/api/download/artist-pfp?id={fullArtist._id}";
                obj += $"{{ \"name\": \"{fullArtist.Name.Replace("\"", "\\\"")}\", \"artwork\": \"{artwork}\", \"value\": \"{album.Value}\"}},";
            }
            obj = obj.Substring(0, obj.Length - 1);
            obj += "]";

            args.SendEvent("Top artists sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }
        [HttpGet("user-page/top-genres")]
        public IActionResult UserPageTopGenres(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-genres", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);


            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "month")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(30));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "year")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(365));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            var stats = StatsCollection.Find(statFilter).ToList();

            var genres = stats.SelectMany(obj => obj.Genres)
                               .GroupBy(x => x)
                               .Select(group => new { Name = group.Key, Count = group.Count() })
                               .OrderByDescending(x => x.Count)
                               .Take(new Range(page * count, (page * count) + count))
                               .ToDictionary(g => g.Name, g => g.Count);



            args.SendEvent("Top genres sent", 200, Program.mWebApi);
            return new ObjectResult(genres) { StatusCode = 200 };
        }
        [HttpGet("user-page/recent-tracks")]
        public IActionResult UserPageRecentTracks(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/recent-tracks", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);


            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x => x.LogDate))
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

            var tracks = recentStats.Select(stat => KeyValuePair.Create( stat.TrackId, stat.LogDate )).ToList();

            List<string> trackIds = (from kvp in tracks select kvp.Key).Distinct().ToList();
            var tracksFull = TracksCollection.Find(x => trackIds.Contains(x._id)).ToList();

            string obj = "[";
            foreach (var track in tracks)
            {
                var fullTrack = tracksFull.FirstOrDefault(x => x._id == track.Key);
                string artists = "";
                foreach (var a in fullTrack.TrackArtists)
                {
                    artists += $"{a.Name},";
                }
                artists = artists.Substring(0, artists.Length - 1);

                string artwork = $"/api/download/track-art?id={fullTrack._id}";
                obj += $"{{ \"name\": \"{fullTrack.Name.Replace("\"", "\\\"")}\", \"artist\": \"{artists}\", \"artwork\": \"{artwork}\", \"value\": \"{track.Value}\"}},";
            }
            obj = obj.Substring(0, obj.Length - 1);
            obj += "]";

            args.SendEvent("Recent tracks sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }
        [HttpGet("user-page/recent-albums")]
        public IActionResult UserPageRecentAlbums(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/recent-albums", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);


            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x => x.LogDate))
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

            var albums = recentStats.Select(stat => KeyValuePair.Create(stat.AlbumId, stat.LogDate)).ToList();

            List<string> albumIds = (from kvp in albums select kvp.Key).Distinct().ToList();
            var albumsFull = AlbumsCollection.Find(x => albumIds.Contains(x._id)).ToList();



            string obj = "[";
            string lastId = "";
            foreach (var album in albums)
            {
                if (lastId == album.Key)
                {
                    continue;
                }
                lastId = album.Key;
                var fullAlbum = albumsFull.FirstOrDefault(x => x._id == album.Key);
                string artists = "";
                foreach (var a in fullAlbum.AlbumArtists)
                {
                    artists += $"{a.Name},";
                }
                artists = artists.Substring(0, artists.Length - 1);

                string artwork = $"/api/download/album-art?id={fullAlbum._id}";
                obj += $"{{ \"name\": \"{fullAlbum.Name.Replace("\"", "\\\"")}\", \"artist\": \"{artists}\", \"artwork\": \"{artwork}\", \"value\": \"{album.Value}\"}},";
            }
            obj = obj.Substring(0, obj.Length - 1);
            obj += "]";

            args.SendEvent("Top genres sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }

        [HttpGet("user-page/recent-artists")]
        public IActionResult UserPageRecentArtists(string userId = "", string timeRange = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/recent-artists", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange },
                { "page", page },
                { "count", count }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");
            userId = userId == "" ? curId : userId;

            var uFilter = Builders<User>.Filter.Eq(x => x._id, userId);
            var user = UsersCollection.Find(uFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User not found", 404, Program.mWebApi);
                return new ObjectResult("User not found") { StatusCode = 404 };
            }

            var statFilter = Builders<PlayStat>.Filter.Regex(x => x.Device, new BsonRegularExpression(device, "i"));
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.Type, "Play");
            if (!user.PublicStats)
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }
            statFilter = statFilter & Builders<PlayStat>.Filter.Eq(x => x.UserId, userId);


            DateTime ltdt = DateTime.UtcNow;
            if (timeRange == "day")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(1));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }
            else if (timeRange == "week")
            {
                DateTime gtdt = ltdt.Subtract(TimeSpan.FromDays(7));
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            var recentStats = StatsCollection.Find(statFilter)
                                             .Sort(Builders<PlayStat>.Sort.Descending(x => x.LogDate))
                                             .Skip(page * count)
                                             .Limit(count)
                                             .ToList();

            //var artists = recentStats.Select(stat => KeyValuePair.Create(stat.ArtistIds, stat.LogDate)).ToList();
            var artists = recentStats.SelectMany(obj => obj.ArtistIds)
                               .GroupBy(x => x)
                               .Select(group => new { Name = group.Key, Count = group.Count() })
                               .OrderByDescending(x => x.Count)
                               .Take(new Range(page * count, (page * count) + count))
                               .ToDictionary(g => g.Name, g => g.Count);

            List<string> artistIds = (from kvp in artists select kvp.Key).Distinct().ToList();
            var artistsFull = ArtistsCollection.Find(x => artistIds.Contains(x._id)).ToList();



            string obj = "[";
            string lastId = "";
            foreach (var artist in artists)
            {
                if (lastId == artist.Key)
                {
                    continue;
                }
                lastId = artist.Key;
                var fullAlbum = artistsFull.FirstOrDefault(x => x._id == artist.Key);

                string artwork = $"/api/download/album-art?id={fullAlbum._id}";
                obj += $"{{ \"name\": \"{fullAlbum.Name.Replace("\"", "\\\"")}\", \"artist\": \"{artists}\", \"artwork\": \"{artwork}\", \"value\": \"{artist.Value}\"}},";
            }
            obj = obj.Substring(0, obj.Length - 1);
            obj += "]";

            args.SendEvent("Top genres sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }
        
        [HttpGet("user-page/listening-time")]
        public IActionResult GetListeningTime(string userId = "",string timeRange = "")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/listening-time", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "timeRange", timeRange }
            });

            // Listening Time Graph

            TimeSpan time = TimeSpan.Zero;
            if (timeRange == "day")
            {
                time = TimeSpan.FromDays(1);
            }
            else if (timeRange == "week")
            {
                time = TimeSpan.FromDays(7);
            }
            else if (timeRange == "month")
            {
                time = TimeSpan.FromDays(30);
            }
            else if (timeRange == "year")
            {
                time = TimeSpan.FromDays(365);
            }

            string vars = "[";
            var dic = GetListeningTimeByHour(time);
            foreach (var item in dic)
            {
                vars += $"{item},";
            }
            vars = vars.Substring(0, vars.Length - 1);
            vars += "]";

            args.SendEvent("Listening time sent", 200, Program.mWebApi);
            return new ObjectResult(vars) { StatusCode = 200 };
        }


        public List<double> GetListeningTimeByHour(TimeSpan timePeriod)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            var endDate = DateTime.UtcNow;
            var startDate = endDate - timePeriod;

            // Get the user's timezone in Windows format (adjust this if you have the timezone ID differently)
            string windowsTimeZoneId = $"{TimeZoneInfo.Local.BaseUtcOffset.Hours.ToString("00")}{TimeZoneInfo.Local.BaseUtcOffset.Minutes.ToString("00")}";

            // Convert Windows timezone ID to IANA timezone ID using TimeZoneConverter
            //string ianaTimeZoneId = TZConvert.WindowsToIana(windowsTimeZoneId);

            var pipeline = new BsonDocument[]
            {
        new BsonDocument("$match", new BsonDocument("LogDate", new BsonDocument
        {
            { "$gte", startDate },
            { "$lte", endDate }
        })),
        new BsonDocument("$group", new BsonDocument
        {
            // Modify the $hour operator to include the user's timezone
            { "_id", new BsonDocument("$hour", new BsonDocument
                {
                    { "date", "$LogDate" },
                    { "timezone", $"{windowsTimeZoneId}" }
                })
            },
            { "totalDuration", new BsonDocument("$sum", new BsonDocument("$toDouble", "$Duration")) }
        })
            };

            var result = StatsCollection.Aggregate<BsonDocument>(pipeline).ToList();

            // Initialize a dictionary to hold the total duration for each hour
            var listeningTimeByHour = new Dictionary<int, double>();
            for (int i = 0; i < 24; i++)
            {
                listeningTimeByHour[i] = 0.0;
            }

            // Process the aggregation result and populate the dictionary
            foreach (var doc in result)
            {
                int hour = doc["_id"].AsInt32;
                double totalDuration = doc["totalDuration"].AsDouble / 1000 / 60; // Convert to minutes
                listeningTimeByHour[hour] = totalDuration;
            }

            // Prepare the final dictionary with formatted time ranges
            var formattedListeningTimeByHour = new List<double>();
            for (int i = 0; i < 24; i++)
            {
                string timeRange = $"{i}:00-{(i + 1) % 24}:00";
                formattedListeningTimeByHour.Add(listeningTimeByHour[i]);
            }

            return formattedListeningTimeByHour;
        }
    }
}