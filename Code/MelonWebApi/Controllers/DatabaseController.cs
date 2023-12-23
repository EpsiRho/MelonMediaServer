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
    [Route("api/db")]
    public class DatabaseController : ControllerBase
    {
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(ILogger<DatabaseController> logger)
        {
            _logger = logger;
        }

        [HttpGet("formats")]
        public async Task<IActionResult> GetFormats(string _id)
        {
            return NotFound();
        }
        
    }
}
