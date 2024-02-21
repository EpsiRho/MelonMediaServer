using Melon.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
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
                return new ObjectResult(Security.GenerateJwtToken(username, user.Type, user._id)) { StatusCode = 200 };
            }
            else
            {
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
            var code = Security.CreateInviteCode();
            if(code == "Timeout")
            {
                return new ObjectResult("Timeout, too many invite codes active") { StatusCode = 408 };
            }
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
            var check = Security.ValidateInviteCode(code);
            if (!check)
            {
                return new ObjectResult("Invalid Invite code") { StatusCode = 401 };
            }
            Security.InvalidateInviteCode(code);
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
