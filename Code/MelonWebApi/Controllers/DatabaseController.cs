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
    [Route("api/db")]
    public class DatabaseController : ControllerBase
    {
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(ILogger<DatabaseController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("formats")]
        public async Task<IActionResult> GetFormats(string _id)
        {
            return NotFound();
        }
        
    }
}
