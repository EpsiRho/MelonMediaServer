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

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/scan")]
    public class ScanController : ControllerBase
    {
        [Authorize(Roles = "Admin")]
        [HttpPost("start")]
        public ObjectResult StartScan(bool skip = false)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/scan/start", curId, new Dictionary<string, object>()
                {
                    { "skip", skip }
                });

            bool started;
            try { started = ServerAdministration.Instance.StartScan(skip); }
            catch (InvalidOperationException error)
            {
                args.SendEvent(error.Message, 409, Program.mWebApi);
                return new ObjectResult(error.Message) { StatusCode = 409 };
            }
            if (!started)
            {
                args.SendEvent("Scanner is already running", 425, Program.mWebApi);
                return new ObjectResult("Scanner is already running") { StatusCode = 425 };
            }

            args.SendEvent("Scanner Started", 202, Program.mWebApi);
            return new ObjectResult("Scanner Started") { StatusCode = 202 };
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("progress")]
        public ObjectResult GetScanProgress()
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/scan/progress-old", curId, new Dictionary<string, object>());

            var status = ServerAdministration.Instance.GetStatus();
            if (!status.Scanning)
            {
                args.SendEvent("Scanner is not running", 204, Program.mWebApi);
                return new ObjectResult("Scanner is not running") { StatusCode = 204 };
            }
            var progress = status.Scan;
            args.SendEvent("Scanner progress sent", 200, Program.mWebApi);
            return new ObjectResult(progress) { StatusCode = 200 };
        }
    }
}
