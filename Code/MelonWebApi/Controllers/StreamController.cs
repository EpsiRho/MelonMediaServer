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
using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Lame;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Primitives;
using DnsClient;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/stream")]
    public class StreamController : ControllerBase
    {
        private readonly ILogger<StreamController> _logger;
        public Dictionary<string, MemoryStream> OpusCache { get; set; } = new Dictionary<string, MemoryStream>();

        public StreamController(ILogger<StreamController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("connect")]
        public async Task ConnectWebSocket()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/connect", curId, new Dictionary<string, object>());

            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var webSocket = HttpContext.WebSockets.AcceptWebSocketAsync().Result;
            StreamManager.AddSocket(webSocket, curId);

            args.SendEvent("Websocket connected", 200, Program.mWebApi);
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("get-external")]
        public ObjectResult GetDevices()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/get-external", curId, new Dictionary<string, object>());

            args.SendEvent("Sent exernal devices", 200, Program.mWebApi);
            return new ObjectResult(StreamManager.GetDevices(curId)){ StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("play-external")]
        public ObjectResult PlayDevice(string deviceName, string queueId)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/play-external", curId, new Dictionary<string, object>()
            {
                { "deviceName", deviceName }, 
                { "queueId", queueId } 
            });
            
            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                args.SendEvent("Device Not Found", 404, Program.mWebApi);
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"PLAY QUEUE:{queueId}");
            args.SendEvent("Sent play to external device", 200, Program.mWebApi);
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("pause-external")]
        public ObjectResult PauseDevice(string deviceName)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/play-external", curId, new Dictionary<string, object>()
            {
                { "deviceName", deviceName }
            });

            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                args.SendEvent("Device Not Found", 404, Program.mWebApi);
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"PAUSE");
            args.SendEvent("Sent pause to external device", 200, Program.mWebApi);
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("skip-external")]
        public ObjectResult SkipDevice(string deviceName)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/play-external", curId, new Dictionary<string, object>()
            {
                { "deviceName", deviceName }
            });

            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                args.SendEvent("Device Not Found", 404, Program.mWebApi);
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"SKIP");
            args.SendEvent("Sent skip to external device", 200, Program.mWebApi);
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("rewind-external")]
        public ObjectResult ReturnDevice(string deviceName)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/rewind-external", curId, new Dictionary<string, object>()
            {
                { "deviceName", deviceName }
            });

            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                args.SendEvent("Device Not Found", 404, Program.mWebApi);
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"REWIND");
            args.SendEvent("Sent rewind to external device", 200, Program.mWebApi);
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User")]
        [HttpGet("volume-external")]
        public ObjectResult SetDeviceVolume(string deviceName, int volume = 50)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/stream/rewind-external", curId, new Dictionary<string, object>()
            {
                { "deviceName", deviceName },
                { "volume", volume },
            });

            var wss = StreamManager.GetDevice(deviceName, curId);
            if (wss == null)
            {
                args.SendEvent("Device Not Found", 404, Program.mWebApi);
                return new ObjectResult("Device Not Found") { StatusCode = 404 };
            }
            StreamManager.WriteToSocket(wss, $"VOLUME:{volume}");
            args.SendEvent("Sent volume to external device", 200, Program.mWebApi);
            return new ObjectResult("Request Sent") { StatusCode = 200 };
        }

    }
}
