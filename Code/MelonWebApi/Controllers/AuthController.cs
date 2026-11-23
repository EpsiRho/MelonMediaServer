using Melon.LocalClasses;
using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text;
using MelonWebApi.Services;


namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("auth/")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private const int MaxInviteMinutes = 30 * 24 * 60; // Galia offers up to 30 days

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Create an invite token for a new user to create an account on this server.
        /// </summary>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// ### Invite Codes:
        /// - Redeemed once with POST auth/register, which creates the account and signs that device in.
        /// </remarks>
        /// <returns>Returns an invite code on success.</returns>
        /// <response code="200">On successful creation of an invite code.</response>
        /// <response code="401">If the user does not have permission to perform this action.</response>
        /// <response code="408">Too many invite codes are active, timeout.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet("invite")]
        public ObjectResult Invite(int expireMinutes = 10)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("auth/invite", curId, new Dictionary<string, object>());

            if (expireMinutes <= 0) return new BadRequestObjectResult("Choose a positive invite lifetime.");
            if (expireMinutes > MaxInviteMinutes) return new BadRequestObjectResult($"Invites can last at most {MaxInviteMinutes / (24 * 60)} days.");
            var code = Security.CreateInviteCode(TimeSpan.FromMinutes(expireMinutes));
            if(code == "Timeout")
            {
                args.SendEvent("Timeout, too many invite codes active", 408, Program.mWebApi);
                return new ObjectResult("Timeout, too many invite codes active") { StatusCode = 408 };
            }

            args.SendEvent("Created invite code", 200, Program.mWebApi);
            return new ObjectResult(code){ StatusCode = 200 };
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
