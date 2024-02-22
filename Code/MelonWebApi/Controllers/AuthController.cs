using Melon.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;


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

        /// <summary>
        /// Login with a username and password to get a JWT for authenticating future api calls.
        /// </summary>
        /// <param name="username">The username to authenticate with.</param>
        /// <param name="password">The password to authenticate with.</param>
        /// <returns>Returns a JWT token on success.</returns>
        /// <response code="200">The username and password match.</response>
        /// <response code="401">If the username isn't found or the password doesn't match.</response>
        [HttpGet("login")]
        public ObjectResult Login(string username, string password)
        {
            var args = new WebApiEventArgs("auth/login", "No Auth", new Dictionary<string, object>());

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");

            var userFilter = Builders<User>.Filter.Eq(x => x.Username, username);
            var users = UserCollection.Find(userFilter).ToList();
            
            if(users.Count == 0) 
            {
                args.SendEvent("Invalid username or password", 401, Program.mWebApi);
                return new ObjectResult("Invalid username or password") { StatusCode = 401 };
            }
            var user = users[0];

            bool check = Security.VerifyPassword(password, user.Password, user.Salt);

            if (check)
            {
                user.LastLogin = DateTime.Now;
                UserCollection.ReplaceOne(userFilter, user);
                args.User = user._id;
                args.SendEvent("User Logged In", 200, Program.mWebApi);
                return new ObjectResult(Security.GenerateJwtToken(username, user.Type, user._id)) { StatusCode = 200 };
            }
            else
            {
                args.SendEvent("Invalid username or password", 401, Program.mWebApi);
                return new ObjectResult("Invalid username or password") { StatusCode = 401 };
            }
        }

        /// <summary>
        /// Create an invite token for a new user to create an account on this server.
        /// </summary>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// ### Invite Codes:
        /// - Last 10 minutes, used to create a JWT token for account creation.
        /// - Use auth/code-authenticate to create a JWT with a "Server" Role. Lasts 10 minutes from creation.
        /// - use api/users/create with the JWT to create the new user
        /// </remarks>
        /// <returns>Returns a four character invite code on success.</returns>
        /// <response code="200">On successful creation of an invite code.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="408">Too many invite codes are active, timeout.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet("invite")]
        public ObjectResult Invite()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("auth/invite", curId, new Dictionary<string, object>());

            var code = Security.CreateInviteCode();
            if(code == "Timeout")
            {
                args.SendEvent("Timeout, too many invite codes active", 408, Program.mWebApi);
                return new ObjectResult("Timeout, too many invite codes active") { StatusCode = 408 };
            }

            args.SendEvent("Created invite code", 200, Program.mWebApi);
            return new ObjectResult(code){ StatusCode = 200 };
        }

        /// <summary>
        /// Get a JWT with the "Server" role using an invite code.
        /// </summary>
        /// <param name="code">The invite code.</param>
        /// <remarks>
        /// ### Server JWT:
        /// - Last 10 minutes, used with api/users/create to create a new user.
        /// - Invalidates the invite code after creating one JWT
        /// </remarks>
        /// <returns>Returns a JWT on success.</returns>
        /// <response code="200">On successful creation of the JWT.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        [HttpGet("code-authenticate")]
        public ObjectResult CodeAuth(string code)
        {
            var args = new WebApiEventArgs("auth/code-authenticate", "No Auth", new Dictionary<string, object>());

            var check = Security.ValidateInviteCode(code);
            if (!check)
            {
                args.SendEvent("Invalid Invite code", 200, Program.mWebApi);
                return new ObjectResult("Invalid invite code") { StatusCode = 401 };
            }
            Security.InvalidateInviteCode(code);

            args.SendEvent("Created Server JWT", 200, Program.mWebApi);
            return new ObjectResult(Security.GenerateJwtToken("NA", "Server", "NA", 10)) { StatusCode = 200 };
        }

        /// <summary>
        /// Test a JWT auth.
        /// </summary>
        /// <returns>Returns an object result indicating the success or failure of the operation.</returns>
        /// <response code="200">If the JWT is valid.</response>
        /// <response code="401">If the JWT is invalid.</response>
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("check")]
        public ObjectResult AuthTest()
        {
            return new ObjectResult("Logged In") { StatusCode = 200 };
        }

    }
}
