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

            using (var webSocket = HttpContext.WebSockets.AcceptWebSocketAsync().Result)
            {
                var buffer = new byte[1024];
                var receiveResult = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).Result;

                while (!receiveResult.CloseStatus.HasValue)
                {
                    webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, receiveResult.Count), receiveResult.MessageType,
                                              receiveResult.EndOfMessage, CancellationToken.None).Wait();

                    receiveResult = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).Result;
                }

                webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, CancellationToken.None).Wait();
            }

        }
        

    }
}
