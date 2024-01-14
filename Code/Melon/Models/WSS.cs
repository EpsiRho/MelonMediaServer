using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class WSS
    {
        public System.Net.WebSockets.WebSocket Socket { get; set; }
        public string CurrentQueue { get; set; }
        public string DeviceName { get; set; }
        public DateTime LastPing { get; set; }

    }
}
