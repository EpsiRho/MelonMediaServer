using Melon.Models;
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
        private static List<WSS> Sockets { get; set; }
        public static void AddSocket(WebSocket socket)
        {
            if(Sockets == null)
            {
                Sockets = new List<WSS>();
            }
            WSS wss = new WSS();
            wss.Socket = socket;
            wss.CurrentQueue = "";
            wss.LastPing = DateTime.Now;
            var check = Sockets.Count() == 0;
            Sockets.Add(wss);
            HandleWebSocketAsync(wss);
            if(check)
            {
                Thread t = new Thread(ManageSockets);
                t.Start();
            }
        }
        public static List<string> GetDevices()
        {
            var devices = new List<string>();
            foreach(var wss in Sockets)
            {
                if(wss.DeviceName != "")
                {
                    devices.Add(wss.DeviceName);
                }
            }
            return devices;
        }
        public static WSS GetDevice(string name)
        {
            var device = new WSS();
            foreach(var wss in Sockets)
            {
                if(wss.DeviceName == name)
                {
                    device = wss;
                    break;
                }
            }
            return device;
        }
        public static void RemoveSocket(WSS wss)
        {
            try
            {
                wss.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
                Sockets.Remove(wss);
            }
            catch (Exception)
            {

            }
        }
        private static async Task HandleWebSocketAsync(WSS wss)
        {
            while (wss.Socket.State == WebSocketState.Open)
            {
                string message = await ReceiveAsync(wss);

                if (message.Contains("PONG"))
                {
                    wss.LastPing = DateTime.Now;
                    Sockets[Sockets.IndexOf(wss)] = wss;
                }
                else if (message.Contains("GET QUEUE"))
                {
                    WriteToSocket(wss, wss.CurrentQueue);
                }
                else if (message.Contains("SET QUEUE"))
                {
                    try
                    {
                        var queueId = message.Split(":")[1];
                        wss.CurrentQueue = queueId;
                        WriteToSocket(wss, wss.CurrentQueue);
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss, "Invalid Syntax");
                    }
                }
                else if (message.Contains("SET DEVICE"))
                {
                    try
                    {
                        var name = message.Split(":")[1];

                        var devices = (from sock in Sockets
                                      where sock.DeviceName == name
                                      select sock).ToList();
                        if(devices.Count() != 0)
                        {
                            WriteToSocket(wss, "Device Name Taken");
                            continue;
                        }

                        wss.DeviceName = name;
                        WriteToSocket(wss, name);
                    }
                    catch (Exception)
                    {
                        WriteToSocket(wss, "Invalid Syntax");
                    }
                }
            }

            RemoveSocket(wss);
        }
        public static void AlertQueueUpdate(string id)
        {
            if(Sockets == null)
            {
                Sockets = new List<WSS>();
            }
            foreach(var wss in Sockets)
            {
                if(wss.CurrentQueue == id)
                {
                    WriteToSocket(wss, "UPDATE QUEUE");
                }
            }
        }
        public static void ManageSockets() 
        {
            while (Sockets.Count() != 0)
            {
                try
                {
                    foreach (WSS wss in Sockets)
                    {
                        if (DateTime.Now - wss.LastPing > new TimeSpan(0, 3, 0))
                        {
                            RemoveSocket(wss);
                        }
                        else if (DateTime.Now - wss.LastPing > new TimeSpan(0, 2, 0))
                        {
                            WriteToSocket(wss, "PING");
                        }
                        Thread.Sleep(1000);
                    }
                }
                catch (Exception)
                {

                }
            }
        }
        public static async void WriteToSocket(WSS wss, string message)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                var segment = new ArraySegment<byte>(buffer);
                await wss.Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception)
            {

            }
        }
        public static async Task<string> ReceiveAsync(WSS wss)
        {
            try
            {
                var buffer = new byte[1024 * 4];
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await wss.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
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
