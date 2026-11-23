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

        /// <summary>Largest message accepted; a bigger one closes the socket (a playback state is a few KB).</summary>
        public const int MaxMessageBytes = 64 * 1024;
        /// <summary>Messages per second one socket may send before it is closed.</summary>
        public const int MaxMessagesPerSecond = 50;
        /// <summary>Longest device name or queue id accepted.</summary>
        public const int MaxNameLength = 128;

        /// <summary>
        /// Registers a client socket and runs it until it disconnects. The returned task must be awaited by the request
        /// (ASP.NET closes the socket when the request ends); the socket is always removed from the device list afterwards.
        /// </summary>
        public static async Task AddSocket(WebSocket socket, string userId, string sessionId = null)
        {
            WSS wss = new WSS();
            wss._id = ObjectId.GenerateNewId().ToString();
            wss.Socket = socket;
            wss.CurrentQueue = "";
            wss.UserId = userId;
            wss.SessionId = sessionId;
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
            bool removed;
            lock (SocketsLock) { removed = Sockets.Remove(wss); }
            if (removed && !string.IsNullOrEmpty(wss.Watching)) NotifyWatchers(wss.Watching);
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
            var windowStart = DateTime.UtcNow;
            int inWindow = 0;
            while (wss.Socket.State == WebSocketState.Open)
            {
                string message = await ReceiveAsync(wss.Socket);
                if (message == null) break; // over MaxMessageBytes: ReceiveAsync already closed the socket
                if ((DateTime.UtcNow - windowStart).TotalSeconds >= 1) { windowStart = DateTime.UtcNow; inWindow = 0; }
                if (++inWindow > MaxMessagesPerSecond)
                {
                    try { await wss.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Too many messages", CancellationToken.None); } catch { }
                    break;
                }
                if (message.Length > 0)
                {
                    wss.LastPing = DateTime.Now; // any traffic proves the client is alive
                }

                if (message.StartsWith("SET STATE:"))
                {
                    // The controlled device's playback state, passed straight through to whoever is controlling it.
                    wss.StateJson = message.Substring("SET STATE:".Length);
                    if (!string.IsNullOrEmpty(wss.DeviceName))
                    {
                        foreach (var w in Watchers(wss))
                        {
                            WriteToSocket(w.Socket, "DEVICE STATE:" + wss.StateJson);
                        }
                    }
                }
                else if (message.StartsWith("WATCH DEVICE:"))
                {
                    var name = message.Substring("WATCH DEVICE:".Length);
                    if (name.Length > MaxNameLength) { WriteToSocket(wss.Socket, "WATCH DENIED"); continue; }
                    var target = GetDevice(name, wss.UserId);
                    var previous = wss.Watching;
                    if (target == null)
                    {
                        wss.Watching = null;
                        WriteToSocket(wss.Socket, "WATCH DENIED");
                    }
                    else
                    {
                        wss.Watching = name;
                        if (!string.IsNullOrEmpty(target.StateJson)) WriteToSocket(wss.Socket, "DEVICE STATE:" + target.StateJson);
                        NotifyWatchers(name);
                    }
                    if (!string.IsNullOrEmpty(previous) && previous != name) NotifyWatchers(previous);
                }
                else if (message.StartsWith("UNWATCH DEVICE"))
                {
                    var previous = wss.Watching;
                    wss.Watching = null;
                    if (!string.IsNullOrEmpty(previous)) NotifyWatchers(previous);
                }
                else if (message == "PONG")
                {
                    wss.LastPing = DateTime.Now;
                }
                else if (message == "GET QUEUE")
                {
                    WriteToSocket(wss.Socket, wss.CurrentQueue);
                }
                else if (message.StartsWith("SET QUEUE:"))
                {
                    try
                    {
                        var queueId = message.Substring("SET QUEUE:".Length);
                        if (queueId.Length == 0 || queueId.Length > MaxNameLength) throw new FormatException();
                        wss.CurrentQueue = queueId;
                        WriteToSocket(wss.Socket, wss.CurrentQueue);
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss.Socket, "Invalid Syntax");
                    }
                }
                else if (message.StartsWith("SET DEVICE:"))
                {
                    try
                    {
                        var name = message.Substring("SET DEVICE:".Length);
                        if (name.Trim().Length == 0 || name.Length > MaxNameLength) throw new FormatException();

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
                        NotifyWatchers(name); // a controller may already be waiting on this name (device reconnected)
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss.Socket, "Invalid Syntax");
                    }
                }
                else if (message.StartsWith("SET PUBLIC:"))
                {
                    try
                    {
                        bool set = bool.Parse(message.Substring("SET PUBLIC:".Length));

                        wss.IsPublic = set;
                        WriteToSocket(wss.Socket, $"DEVICE IS PUBLIC:{set}");
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss.Socket, "Invalid Syntax");
                    }
                }
                else if (message == "SEND PROGRESS")
                {
                    Task.Run(()=> { SendProgress(wss); });
                }
                else if (message == "STOP PROGRESS")
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
        /// <summary>Live sockets watching this device that are still allowed to see it.</summary>
        private static List<WSS> Watchers(WSS device)
        {
            return Snapshot().Where(w => w != device && w.Watching == device.DeviceName && w.Socket.State == WebSocketState.Open
                                         && (w.UserId == device.UserId || device.IsPublic)).ToList();
        }

        /// <summary>Tells a device how many controllers are watching it, so it only streams its state when someone is.</summary>
        private static void NotifyWatchers(string deviceName)
        {
            foreach (var dev in Snapshot().Where(d => d.DeviceName == deviceName && d.Socket.State == WebSocketState.Open))
            {
                WriteToSocket(dev.Socket, $"WATCHERS:{Watchers(dev).Count}");
            }
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
                    // A socket outlives the token it was opened with: close it once its device is signed out.
                    if (wss.SessionId != null && !AuthSessions.IsActive(wss.SessionId, wss.UserId))
                    {
                        try { await wss.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Signed out", CancellationToken.None); } catch { }
                        RemoveSocket(wss);
                        break;
                    }
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
        // A WebSocket allows one SendAsync at a time; state relays now overlap other messages, so sends are queued per socket.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WebSocket, SemaphoreSlim> SendLocks = new();
        public static async void WriteToSocket(WebSocket ws, string message)
        {
            var gate = SendLocks.GetValue(ws, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception)
            {

            }
            finally
            {
                gate.Release();
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
                        if (ms.Length + result.Count > MaxMessageBytes)
                        {
                            try { await ws.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too big", CancellationToken.None); } catch { }
                            return null;
                        }
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
