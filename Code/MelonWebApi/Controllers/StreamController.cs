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
            StreamManager.AddSocket(webSocket);

            while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {

            }
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("get-external")]
        public ObjectResult GetDevices()
        {
            return new ObjectResult(StreamManager.GetDevices()){ StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("play-external")]
        public ObjectResult PlayDevice(string deviceName, string queueId)
        {
            var wss = StreamManager.GetDevice(deviceName);
            if (wss == null)
            {
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"PLAY QUEUE:{queueId}");
            return new ObjectResult("Play Request Sent") { StatusCode = 200 };
        }

    }
}
