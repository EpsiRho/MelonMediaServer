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
using System.Security.Claims;
using System.Data;
using RestSharp;
using Azure.Core;
using RestSharp.Authenticators;
using System.Text;
using System.Linq;
using NAudio.CoreAudioApi;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;

        public UserController(ILogger<UserController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("get")]
        public ObjectResult GetById(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            var args = new WebApiEventArgs("api/users/get", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var user = UserCollection.Find(userFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if (user._id != curId)
            {
                if (!user.PublicStats && !roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            ResponseUser pUser = new ResponseUser(user);

            args.SendEvent("User info sent", 200, Program.mWebApi);
            return new ObjectResult(pUser) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("search")]
        public ObjectResult SearchUsers(string username = "")
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var roles = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.Role)
                       .Select(c => c.Value);
            var args = new WebApiEventArgs("api/users/search", curId, new Dictionary<string, object>()
            {
                { "username", username }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var currentUserFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var currentUsers = UserCollection.Find(currentUserFilter).ToList();

            if (currentUsers.Count == 0)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var cUser = currentUsers[0];

            var userFilter = Builders<User>.Filter.Regex(x => x.Username, new BsonRegularExpression(username, "i"));
            var users = UserCollection.Find(userFilter).ToList();

            List<ResponseUser> pUsers = new List<ResponseUser>();
            foreach (var user in users)
            {
                if (user.Friends == null)
                {
                    user.Friends = new List<string>();
                }

                if (user.Username != User.Identity.Name)
                {
                    if (!user.Friends.Contains(cUser._id) && !roles.Contains("Admin"))
                    {
                        continue;
                    }
                }
                pUsers.Add(new ResponseUser(user));
            }

            args.SendEvent("Users sent", 200, Program.mWebApi);
            return new ObjectResult(pUsers) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("add-friend")]
        public ObjectResult AddFriend(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            var args = new WebApiEventArgs("api/users/add-friend", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var user = UserCollection.Find(userFilter).FirstOrDefault();
            if (user == null)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if (user.Friends == null)
            {
                user.Friends = new List<string>();
            }

            if (user._id != curId)
            {
                if (!roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            user.Friends.Add(id);

            UserCollection.ReplaceOne(userFilter, user);

            args.SendEvent("Friend Added", 404, Program.mWebApi);
            return new ObjectResult("Friend Added") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPost("remove-friend")]
        public ObjectResult RemoveFriend(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            var args = new WebApiEventArgs("api/users/remove-friend", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var users = UserCollection.Find(userFilter).ToList();
            var user = users.FirstOrDefault();
            if (user == null)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if (user._id != curId)
            {
                if (!roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            if (user.Friends == null)
            {
                user.Friends = new List<string>();
            }

            user.Friends.Remove(id);

            UserCollection.ReplaceOne(userFilter, user);

            args.SendEvent("Friend Removed", 200, Program.mWebApi);
            return new ObjectResult("Friend Removed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("current")]
        public ObjectResult GetCurrent()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var role = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/users/current", curId, new Dictionary<string, object>());

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, User.Identity.Name);
            var user = UserCollection.Find(userFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            ResponseUser pUser = new ResponseUser(user);

            args.SendEvent("User sent", 200, Program.mWebApi);
            return new ObjectResult(pUser) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,Server")]
        [HttpPost("create")]
        public ObjectResult CreateUser(string username, string password, string role = "User")
        {
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            if (roles.Contains("Server"))
            {
                role = "User";
            }

            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/users/create", curId, new Dictionary<string, object>()
            {
                { "username", username },
                { "role", role },
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            if (password == "")
            {
                args.SendEvent("User Not Found", 400, Program.mWebApi);
                return new ObjectResult("Password cannot be empty") { StatusCode = 400 };
            }

            byte[] tempSalt;
            var protectedPassword = Security.HashPassword(password, out tempSalt);

            var user = new User();
            user._id = ObjectId.GenerateNewId().ToString();
            user.Username = username;
            user.Password = protectedPassword;
            user.Salt = tempSalt;
            user.Type = role;
            user.Bio = "";
            user.FavTrack = "";
            user.FavAlbum = "";
            user.FavArtist = "";
            user.LastLogin = DateTime.MinValue;

            UserCollection.InsertOne(user);

            args.SendEvent("User created", 200, Program.mWebApi);
            return new ObjectResult(user._id) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("delete")]
        public ObjectResult Delete(string id)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);
            var args = new WebApiEventArgs("api/users/delete", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var user = UserCollection.Find(userFilter).FirstOrDefault();

            if (user == null)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            if (user._id != curId)
            {
                if (!roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            UserCollection.DeleteOne(userFilter);

            args.SendEvent("User deleted", 200, Program.mWebApi);
            return new ObjectResult("User Deleted") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("update")]
        public ObjectResult Update(string id, string bio = null, string role = null, string publicStats = null, string favTrackId = null, string favAlbumId = null, string favArtistId = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/users/update", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "bio", bio },
                { "role", role },
                { "publicStats", publicStats },
                { "favTrackId", favTrackId },
                { "favAlbumId", favAlbumId },
                { "favArtistId", favArtistId },
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();


            if (users.Count == 0)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);

            if (user._id != curId)
            {
                if (!roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }


            if (bio != null)
            {
                user.Bio = bio;
            }
            if (role != null && roles.Contains("Admin"))
            {
                user.Type = role;
            }
            else if (role != null && !roles.Contains("Admin"))
            {
                args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                return new ObjectResult("Invalid Auth") { StatusCode = 401 };
            }

            if (publicStats != null)
            {
                user.PublicStats = Convert.ToBoolean(publicStats);
            }
            if (favTrackId != null)
            {
                user.FavTrack = favTrackId;
            }
            if (favAlbumId != null)
            {
                user.FavAlbum = favAlbumId;
            }
            if (favArtistId != null)
            {
                user.FavArtist = favArtistId;
            }

            UserCollection.ReplaceOne(userFilter, user);

            args.SendEvent("User updated", 200, Program.mWebApi);
            return new ObjectResult("User Updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("change-username")]
        public ObjectResult ChangeUsername(string id, string username)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/users/change-username", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "username", username }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();


            if (users.Count == 0)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }

            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);

            if (user._id != curId)
            {
                if (!roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 404, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            user.Username = username;

            UserCollection.ReplaceOne(userFilter, user);

            args.SendEvent("Username Changed", 200, Program.mWebApi);
            return new ObjectResult("Username Changed") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpPatch("change-password")]
        public ObjectResult ChangePassword(string id, string password)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/users/change-password", curId, new Dictionary<string, object>()
            {
                { "id", id }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x._id, id);
            var users = UserCollection.Find(userFilter).ToList();

            if (users.Count == 0)
            {
                args.SendEvent("User Not Found", 404, Program.mWebApi);
                return new ObjectResult("User Not Found") { StatusCode = 404 };
            }
            var user = users[0];
            var roles = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value);

            if (user._id != curId)
            {
                if (!roles.Contains("Admin"))
                {
                    args.SendEvent("Invalid Auth", 401, Program.mWebApi);
                    return new ObjectResult("Invalid Auth") { StatusCode = 401 };
                }
            }

            byte[] tempSalt;
            var protectedPassword = Security.HashPassword(password, out tempSalt);

            user.Password = protectedPassword;
            user.Salt = tempSalt;

            UserCollection.ReplaceOne(userFilter, user);

            args.SendEvent("Password Changed", 200, Program.mWebApi);
            return new ObjectResult("Password Changed") { StatusCode = 200 };
        }

    }
}
