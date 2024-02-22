using Melon.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class WebApiEventArgs : EventArgs
    {
        public string Api { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string User { get; set; }
        public Dictionary<string, object> Args { get; set; }
        public WebApiEventArgs(string api, string user, Dictionary<string,object> args)
        {
            Api = api;
            User = user;
            Args = args;
            StatusCode = 0;
            Message = "";
        }
        public void SendEvent(string message, int statusCode, MWebApi webApi)
        {
            Message = message;
            StatusCode = statusCode;
            webApi.OnApiCall(this);
        }
    }
}
