using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("auth/")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        [HttpGet("login")]
        public string Login(string username, string password)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(username, "i"));
            var users = UserCollection.Find(userFilter).ToList();
            
            if(users.Count == 0) 
            {
                return "Invalid username or password";
            }
            var user = users[0];

            bool check = Security.VerifyPassword(password, user.Password, user.Salt);

            if (check)
            {
                return Security.GenerateJwtToken(username, user.Type, 60);
            }
            else
            {
                return "Invalid username or password";
            }
        }

    }
}
