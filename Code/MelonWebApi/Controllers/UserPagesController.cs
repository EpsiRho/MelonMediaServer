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

            // Add Favorites
            var FavTrack = TracksCollection.Find(Builders<Track>.Filter.Eq(x => x._id, user.FavTrack)).FirstOrDefault();
            var FavAlbum = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, user.FavAlbum)).FirstOrDefault();
            var FavArtist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, user.FavArtist)).FirstOrDefault();

            vars += "const favorites = {\n";
            if (FavTrack != null)
            {
                ATL.Track file = null;
                byte[] image = null;
                try
                {
                    file = new ATL.Track(FavTrack.Path);
                    var pic = file.EmbeddedPictures[FavTrack.TrackArtDefault];
                    image = pic.PictureData;
                }
                catch (Exception)
                {
                    image = StateManager.GetDefaultImage();
                }
                string imageBase64 = Convert.ToBase64String(image);
                vars += $"\ttrack: {{\n\t\tname: \"{FavTrack.Name}\",\n\t\tartist: \"{String.Join(", ", FavTrack.TrackArtists.Select(x => x.Name).ToArray())}\",\n\t\tartwork: \"data:image/jpeg;base64,{imageBase64}\"}},\n\t";
            }
            if (FavAlbum != null)
            {
                byte[] image = null;
                try
                {
                    FileStream file = new FileStream($"{StateManager.melonPath}/AlbumArts/{FavAlbum.AlbumArtPaths[FavAlbum.AlbumArtDefault]}", FileMode.Open, FileAccess.Read);
                    image = new byte[file.Length];
                    file.Read(image, 0, (int)file.Length);
                }
                catch (Exception)
                {
                    image = StateManager.GetDefaultImage();
                }
                string imageBase64 = Convert.ToBase64String(image);
                vars += $"\talbum: {{\n\t\tname: \"{FavAlbum.Name}\",\n\t\tartist: \"{String.Join(", ", FavAlbum.AlbumArtists.Select(x => x.Name).ToArray())}\",\n\t\tartwork: \"data:image/jpeg;base64,{imageBase64}\"}},\n\t";
            }
            if (FavArtist != null)
            {
                byte[] image = null;
                try
                {
                    FileStream file = new FileStream($"{StateManager.melonPath}/ArtistPfps/{FavAlbum.AlbumArtPaths[FavAlbum.AlbumArtDefault]}", FileMode.Open, FileAccess.Read);
                    image = new byte[file.Length];
                    file.Read(image, 0, (int)file.Length);
                }
                catch (Exception)
                {
                    image = StateManager.GetDefaultImage();
                }
                string imageBase64 = Convert.ToBase64String(image);
                vars += $"\tartist: {{\n\t\tname: \"{FavArtist.Name}\",\n\t\tartwork: \"data:image/jpeg;base64,{imageBase64}\"\n\t}}";
            }

            vars += "};\n";

            // User Profile
            byte[] userpfp = null;
            try
            {
                FileStream file = new FileStream($"{StateManager.melonPath}/UserPfps/{user._id}.png", FileMode.Open, FileAccess.Read);
                userpfp = new byte[file.Length];
                file.Read(userpfp, 0, (int)file.Length);
            }
            catch (Exception)
            {
                userpfp = StateManager.GetDefaultImage();
            }
            string userpfpBase64 = Convert.ToBase64String(userpfp);
            vars += $"const userProfile = {{\n\tusername: \"{user.Username}\",\n\tprofilePicture: \"data:image/jpeg;base64,{userpfpBase64}\",\n\tbio: \"{user.Bio}\"\n}};\n";

            // Listening Time Graph
            vars += "const listeningTimeStats = [\n";
            var dic = GetListeningTimeByHour(TimeSpan.FromDays(2));
            foreach (var item in dic)
            {
                vars += $"{{ \"{item.Key}\": {item.Value} }},\n";
            }
            vars += "];\n";

            var htmlContent = System.IO.File.ReadAllText($"{_environment.WebRootPath}/index.html");

            htmlContent = htmlContent.Replace("VariablesInputHere", vars);

            return Content(htmlContent, "text/html");
        }
        [HttpGet("user-page/top-tracks")]
        public IActionResult UserPageTopTracks(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-tracks", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "ltDateTime", ltDateTime },
                { "gtDateTime", gtDateTime },
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


            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
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
        public IActionResult UserPageTopAlbums(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-albums", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "ltDateTime", ltDateTime },
                { "gtDateTime", gtDateTime },
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


            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
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
        public IActionResult UserPageTopArtists(string userId = "", string ltDateTime = "", string gtDateTime = "", string device = "", int page = 0, int count = 100)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("user-page/top-artists", "Stats", new Dictionary<string, object>()
            {
                { "userId", userId },
                { "ltDateTime", ltDateTime },
                { "gtDateTime", gtDateTime },
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


            if (ltDateTime != "")
            {
                DateTime ltdt = DateTime.Parse(ltDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Lte(x => x.LogDate, ltdt);
            }

            if (gtDateTime != "")
            {
                DateTime gtdt = DateTime.Parse(gtDateTime);
                statFilter = statFilter & Builders<PlayStat>.Filter.Gte(x => x.LogDate, gtdt);
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

            args.SendEvent("Top tracks sent", 200, Program.mWebApi);
            return new ObjectResult(obj) { StatusCode = 200 };
        }
        public Dictionary<string, double> GetListeningTimeByHour(TimeSpan timePeriod)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var StatsCollection = mongoDatabase.GetCollection<PlayStat>("Stats");

            var endDate = DateTime.UtcNow.ToUniversalTime();
            var startDate = endDate - timePeriod;

            var pipeline = new BsonDocument[]
            {
            new BsonDocument("$match", new BsonDocument("LogDate", new BsonDocument
            {
                { "$gte", startDate },
                { "$lte", endDate }
            })),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$hour", "$LogDate") },
                { "totalDuration", new BsonDocument("$sum", new BsonDocument("$toDouble", "$Duration")) }
            })
            };

            var result = StatsCollection.Aggregate<BsonDocument>(pipeline).ToList();

            var listeningTimeByHour = new Dictionary<string, double>();
            for (int i = 0; i < 22; i++)
            {
                double totalDuration = 0;
                for (int j = 0; j < result.Count; j++)
                {
                    if (result[j]["_id"].AsInt32 == i)
                    {
                        totalDuration = result[j]["totalDuration"].AsDouble;
                    }
                }

                listeningTimeByHour.Add($"{i}:00-{i + 1}:00", totalDuration / 1000 / 60);
            }

            return listeningTimeByHour;
        }
    }
}