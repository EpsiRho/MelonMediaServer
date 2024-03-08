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
using NuGet.Packaging.Signing;
using System.Security.Claims;
using Melon.Classes;
using Pastel;
using Melon.DisplayClasses;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/scan")]
    public class ScanController : ControllerBase
    {
        private readonly ILogger<ScanController> _logger;

        public ScanController(ILogger<ScanController> logger)
        {
            _logger = logger;
        }

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

            if (MelonScanner.Scanning) 
            {
                args.SendEvent("Scanner is already running", 425, Program.mWebApi);
                return new ObjectResult("Scanner is already running") { StatusCode = 425 };
            }

            DisplayManager.UIExtensions.Add("LibraryScanIndicator", () =>
            {
                Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight));
            });
            MelonUI.endOptionsDisplay = true;
            Thread scanThread = new Thread(MelonScanner.StartScan);
            scanThread.Start(skip);

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

            if (!MelonScanner.Scanning)
            {
                args.SendEvent("Scanner is not running", 204, Program.mWebApi);
                return new ObjectResult("Scanner is not running") { StatusCode = 204 };
            }

            var progress = new ScanProgress()
            {
                ScannedFiles = MelonScanner.ScannedFiles,
                FoundFiles = MelonScanner.FoundFiles,
                Status = MelonScanner.CurrentStatus
            };

            args.SendEvent("Scanner progress sent", 200, Program.mWebApi);
            return new ObjectResult(progress) { StatusCode = 200 };
        }
    }
}
