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
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(ILogger<DownloadController> logger)
        {
            _logger = logger;
        }

        [HttpGet("track")]
        public async Task<IActionResult> DownloadTrack(string _id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");


            var tFilter = Builders<Track>.Filter.Eq("_id", ObjectId.Parse(_id));
            var track = TCollection.Find(tFilter).ToList()[0];

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
                return NotFound(); 

            string filename = Path.GetFileName(track.Path);
            return File(fileStream, "application/octet-stream", $"{filename}"); 
        }
        [HttpGet("trackArt")]
        public async Task<IActionResult> DownloadTrackArt(string _id, int index)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");


            var tFilter = Builders<Track>.Filter.Eq("_id", ObjectId.Parse(_id));
            var track = TCollection.Find(tFilter).ToList()[0];

            //FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            //if (fileStream == null)
            //    return NotFound(); // returns a NotFoundResult with Status404NotFound response.

            ATL.Track file = null;
            try
            {
                file = new ATL.Track(track.Path);
            }
            catch (Exception)
            {
                return NotFound();
            }
            try 
            { 
                // Load image data in MemoryStream
                var pic = file.EmbeddedPictures[index];
                MemoryStream ms = new MemoryStream(pic.PictureData);
                ms.Seek(0, SeekOrigin.Begin);

                return File(ms, $"image/jpeg");
                

            }
            catch(Exception)
            {
                // TODO: Handle tracks with no image
                return NotFound();
            }
            
        }
        [HttpGet("albumArt")]
        public async Task<IActionResult> DownloadAlbumArt(string _id, int index)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var ACollection = mongoDatabase.GetCollection<Album>("Albums");


            try
            {
                var aFilter = Builders<Album>.Filter.Eq("_id", ObjectId.Parse(_id));
                var album = ACollection.Find(aFilter).ToList()[0];

                // Load image data in MemoryStream
                //MemoryStream ms = new MemoryStream();
                FileStream file = new FileStream(album.AlbumArtPaths[index], FileMode.Open, FileAccess.Read);
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                //ms.Write(bytes, 0, (int)file.Length);
                return File(bytes, "image/jpeg");
            }
            catch (Exception)
            {
                return NotFound();
            }


        }
    }
}
