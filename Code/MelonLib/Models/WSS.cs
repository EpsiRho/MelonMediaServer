using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class WSS
    {
        public string _id { get; set; }
        public System.Net.WebSockets.WebSocket Socket { get; set; }
        public string CurrentQueue { get; set; }
        public string DeviceName { get; set; }
        public string UserId { get; set; }
        /// <summary>Sign-in session the socket was opened with (null for legacy tokens); the socket closes when it is revoked.</summary>
        public string SessionId { get; set; }
        public bool IsPublic { get; set; }
        public bool SendProgress { get;set; }
        public DateTime LastPing { get; set; }
        /// <summary>Latest playback state this device reported ("SET STATE:{json}"), relayed to controllers watching it.</summary>
        public string StateJson { get; set; }
        /// <summary>Device this socket is controlling and wants state updates from ("WATCH DEVICE:name").</summary>
        public string Watching { get; set; }

    }
}
