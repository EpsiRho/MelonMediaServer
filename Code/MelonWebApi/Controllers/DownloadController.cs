using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;

        public DownloadController(ILogger<SearchController> logger)
        {
            _logger = logger;
        }

        [HttpGet("track")]
        public async Task<IActionResult> Download(string _id)
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017");

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");


            //var str = queue._id.ToString();
            var tFilter = Builders<Track>.Filter.Eq("_id", ObjectId.Parse(_id));
            var track = TCollection.Find(tFilter).ToList()[0];

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
                return NotFound(); // returns a NotFoundResult with Status404NotFound response.

            string filename = Path.GetFileName(track.Path);
            return File(fileStream, "application/octet-stream", $"{filename}"); // returns a FileStreamResult
        }
    }
}
