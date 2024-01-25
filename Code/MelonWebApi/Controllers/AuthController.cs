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
        public ObjectResult Login(string username, string password)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, username);
            var users = UserCollection.Find(userFilter).ToList();
            
            if(users.Count == 0) 
            {
                return new ObjectResult("Invalid username or password") { StatusCode = 401 };
            }
            var user = users[0];

            bool check = Security.VerifyPassword(password, user.Password, user.Salt);

            if (check)
            {
                user.LastLogin = DateTime.Now;
                UserCollection.ReplaceOne(userFilter, user);
                return new ObjectResult(Security.GenerateJwtToken(username, user.Type)) { StatusCode = 200 };
            }
            else
            {
                return new ObjectResult("Invalid username or password") { StatusCode = 401 };
            }
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("invite")]
        public ObjectResult ServerAuth()
        {
            var code = Security.CreateInviteCode();
            if(code == "Timeout")
            {
                return new ObjectResult("Timeout, too many invite codes active") { StatusCode = 408 };
            }
            return new ObjectResult(code){ StatusCode = 200 };
        }
        [HttpGet("code-authenticate")]
        public ObjectResult CodeAuth(string code)
        {
            var check = Security.ValidateInviteCode(code);
            if (!check)
            {
                return new ObjectResult("Invalid Invite code") { StatusCode = 401 };
            }
            return new ObjectResult(Security.GenerateJwtToken("ServerTemp", "Server", 10)) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("check")]
        public ObjectResult AuthTest()
        {
            return new ObjectResult("Logged In") { StatusCode = 200 };
        }

    }
}
