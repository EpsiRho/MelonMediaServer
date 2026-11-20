using Melon.Models;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Melon.LocalClasses
{
    public static class StreamManager
    {
        private static List<WSS> Sockets { get; set; } = new List<WSS>();
        private static readonly object SocketsLock = new object();
        private static List<WSS> Snapshot() { lock (SocketsLock) { return Sockets.ToList(); } }

        /// <summary>
        /// Registers a client socket and runs it until it disconnects. The returned task must be awaited by the request
        /// (ASP.NET closes the socket when the request ends); the socket is always removed from the device list afterwards.
        /// </summary>
        public static async Task AddSocket(WebSocket socket, string userId)
        {
            WSS wss = new WSS();
            wss._id = ObjectId.GenerateNewId().ToString();
            wss.Socket = socket;
            wss.CurrentQueue = "";
            wss.UserId = userId;
            wss.IsPublic = false;
            wss.LastPing = DateTime.Now;
            lock (SocketsLock) { Sockets.Add(wss); }
            try
            {
                await Task.WhenAll(HandleWebSocketAsync(wss), ManageSocket(wss));
            }
            finally
            {
                RemoveSocket(wss);
            }
        }
        public static List<string> GetDevices(string userId)
        {
            // Only live sockets that have named themselves; dead ones are dropped here too in case a close was missed.
            var devices = new List<string>();
            foreach(var wss in Snapshot())
            {
                if (wss.Socket.State != WebSocketState.Open) { RemoveSocket(wss); continue; }
                if (string.IsNullOrWhiteSpace(wss.DeviceName)) continue;
                if(wss.UserId == userId || wss.IsPublic == true)
                {
                    devices.Add(wss.DeviceName);
                }
            }
            return devices.Distinct().ToList();
        }
        public static WSS GetDevice(string name, string userId)
        {
            foreach(var wss in Snapshot())
            {
                if(wss.DeviceName == name && wss.Socket.State == WebSocketState.Open)
                {
                    if(userId != wss.UserId && wss.IsPublic == false)
                    {
                        return null;
                    }

                    return wss;
                }
            }
            return null;
        }
        public static void RemoveSocket(WSS wss)
        {
            // Remove first: closing an aborted socket throws, which used to leave dead devices listed forever.
            lock (SocketsLock) { Sockets.Remove(wss); }
            try
            {
                wss.SendProgress = false;
                if (wss.Socket.State == WebSocketState.Open || wss.Socket.State == WebSocketState.CloseReceived)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    wss.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token).Wait();
                }
            }
            catch (Exception)
            {

            }
        }
        private static async Task HandleWebSocketAsync(WSS wss)
        {
            while (wss.Socket.State == WebSocketState.Open)
            {
                string message = await ReceiveAsync(wss.Socket);
                if (message.Length > 0)
                {
                    wss.LastPing = DateTime.Now; // any traffic proves the client is alive
                }

                if (message.Contains("PONG"))
                {
                    wss.LastPing = DateTime.Now;
                }
                else if (message.Contains("GET QUEUE"))
                {
                    WriteToSocket(wss.Socket, wss.CurrentQueue);
                }
                else if (message.Contains("SET QUEUE"))
                {
                    try
                    {
                        var queueId = message.Split(":")[1];
                        wss.CurrentQueue = queueId;
                        WriteToSocket(wss.Socket, wss.CurrentQueue);
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss.Socket, "Invalid Syntax");
                    }
                }
                else if (message.Contains("SET DEVICE"))
                {
                    try
                    {
                        var name = message.Split(":")[1];

                        var devices = (from sock in Snapshot()
                                      where sock != wss && sock.DeviceName == name
                                      select sock).ToList();
                        // The same user reconnecting (restart, crash, network drop) replaces its old entry instead of
                        // being told the name is taken; another user's live device keeps the name.
                        foreach (var old in devices.Where(d => d.UserId == wss.UserId || d.Socket.State != WebSocketState.Open))
                        {
                            RemoveSocket(old);
                        }
                        if(devices.Any(d => d.UserId != wss.UserId && d.Socket.State == WebSocketState.Open))
                        {
                            WriteToSocket(wss.Socket, "Device Name Taken");
                            continue;
                        }

                        wss.DeviceName = name;
                        WriteToSocket(wss.Socket, name);
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss.Socket, "Invalid Syntax");
                    }
                }
                else if (message.Contains("SET PUBLIC"))
                {
                    try
                    {
                        bool set = bool.Parse(message.Split(":")[1]);

                        wss.IsPublic = set;
                        WriteToSocket(wss.Socket, $"DEVICE IS PUBLIC:{set}");
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss.Socket, "Invalid Syntax");
                    }
                }
                else if (message.Contains("SEND PROGRESS"))
                {
                    Task.Run(()=> { SendProgress(wss); });
                }
                else if (message.Contains("STOP PROGRESS"))
                {
                    wss.SendProgress = false;
                }
            }

            RemoveSocket(wss);
        }
        public static void SendProgress(WSS wss)
        {
            if(wss == null)
            {
                return;
            }

            wss.SendProgress = true;
            string lastStatus = "";
            while (wss.SendProgress && MelonScanner.Scanning)
            {
                var ScannedFiles = MelonScanner.ScannedFiles;
                var FoundFiles = MelonScanner.FoundFiles;
                var CurrentStatus = MelonScanner.CurrentStatus;

                WriteToSocket(wss.Socket, $"PROGRESS:{ScannedFiles}:{FoundFiles}");
                if(lastStatus != CurrentStatus)
                {
                    lastStatus = CurrentStatus;
                    WriteToSocket(wss.Socket, $"PROGRESS INFO:{CurrentStatus}");
                }
                Thread.Sleep(100);
            }
            WriteToSocket(wss.Socket, $"END PROGRESS");
        }
        public static void AlertQueueUpdate(string id, string msg = "UPDATE QUEUE", string skipDevice = "")
        {
            foreach(var wss in Snapshot())
            {
                if(wss.CurrentQueue == id && wss.DeviceName != skipDevice)
                {
                    WriteToSocket(wss.Socket, msg);
                }
            }
        }
        public static async Task ManageSocket(WSS wss)
        {
            while (Snapshot().Any(x=>x._id == wss._id))
            {
                try
                {
                    if (wss.Socket.State != WebSocketState.Open || DateTime.Now - wss.LastPing > new TimeSpan(0, 3, 0))
                    {
                        RemoveSocket(wss);
                        break;
                    }
                    else if (DateTime.Now - wss.LastPing > new TimeSpan(0, 2, 0))
                    {
                        WriteToSocket(wss.Socket, "PING");
                    }
                }
                catch (Exception)
                {

                }
                await Task.Delay(2000);
            }
        }
        public static async void WriteToSocket(WebSocket ws, string message)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception)
            {

            }
        }
        public static async Task<string> ReceiveAsync(WebSocket ws)
        {
            try
            {
                var buffer = new byte[1024 * 4];
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            return await reader.ReadToEndAsync();
                        }
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
