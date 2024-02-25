using Melon.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MongoDB.Bson;


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

            if(user == null)
            {
                return Content("User Not Found", "text/html");
            }

            string vars = "";

            // Add Favorites
            var FavTrack = TracksCollection.Find(Builders<Track>.Filter.Eq(x => x._id, user.FavTrack)).FirstOrDefault();
            var FavAlbum = AlbumsCollection.Find(Builders<Album>.Filter.Eq(x => x._id, user.FavAlbum)).FirstOrDefault();
            var FavArtist = ArtistsCollection.Find(Builders<Artist>.Filter.Eq(x => x._id, user.FavArtist)).FirstOrDefault();

            vars += "const favorites = {\n";
            if(FavTrack != null)
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
            if(FavAlbum != null)
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
            if(FavArtist != null)
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
                FileStream file = new FileStream($"{StateManager.melonPath}/UserPfps/{FavAlbum.AlbumArtPaths[FavAlbum.AlbumArtDefault]}", FileMode.Open, FileAccess.Read);
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
            for(int i = 0; i < 22; i++)
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
