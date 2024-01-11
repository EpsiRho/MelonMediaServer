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
            if(MelonScanner.Scanning) 
            {
                return new ObjectResult("Scanner is already running") { StatusCode = 425 };
            } 

            Thread scanThread = new Thread(MelonScanner.StartScan);
            scanThread.Start(skip);

            return new ObjectResult("Scanner Started") { StatusCode = 202 };
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("progress")]
        public ObjectResult GetScanProgress()
        {
            if (!MelonScanner.Scanning)
            {
                return new ObjectResult("Scanner is not running") { StatusCode = 204 };
            }

            var progress = new ScanProgress()
            {
                estimatedTimeLeft = (MelonScanner.averageMilliseconds / MelonScanner.ScannedFiles) * (MelonScanner.FoundFiles - MelonScanner.ScannedFiles),
                averageMillisecondsPerFile = MelonScanner.averageMilliseconds / MelonScanner.ScannedFiles,
                ScannedFiles = MelonScanner.ScannedFiles,
                FoundFiles = MelonScanner.FoundFiles
            };

            return new ObjectResult(progress) { StatusCode = 200 };
        }
    }
}
