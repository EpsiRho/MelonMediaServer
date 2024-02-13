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
using System.IO;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/stream")]
    public class StreamController : ControllerBase
    {
        private readonly ILogger<StreamController> _logger;

        public StreamController(ILogger<StreamController> logger)
        {
            _logger = logger;
        }

        [Route("connect")]
        public async Task ConnectWebSocket()
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var webSocket = HttpContext.WebSockets.AcceptWebSocketAsync().Result;
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            StreamManager.AddSocket(webSocket, curId);

            while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {

            }
        }
        [Route("track")]
        public async Task<IActionResult> StreamTrack(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var track = TCollection.Find(tFilter).FirstOrDefault();
            if (track == null)
            {
                return NotFound();
            }

            FileStream fileStream = new FileStream(track.Path, FileMode.Open, FileAccess.Read);

            if (fileStream == null)
            {
                return NotFound();
            }

            string filename = Path.GetFileName(track.Path);

            return new FileStreamResult(fileStream, $"audio/{track.Format.Replace(".","")}")
            {
                EnableRangeProcessing = true
            };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("get-external")]
        public ObjectResult GetDevices()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            return new ObjectResult(StreamManager.GetDevices(curId)){ StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("play-external")]
        public ObjectResult PlayDevice(string deviceName, string queueId)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"PLAY QUEUE:{queueId}");
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("pause-external")]
        public ObjectResult PauseDevice(string deviceName)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"PAUSE");
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("skip-external")]
        public ObjectResult SkipDevice(string deviceName)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"SKIP");
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("rewind-external")]
        public ObjectResult ReturnDevice(string deviceName)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"REWIND");
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("volume-external")]
        public ObjectResult SetDeviceVolume(string deviceName, int volume = 50)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"VOLUME:{volume}");
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }

    }
}
