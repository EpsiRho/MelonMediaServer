using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Melon.Models;
using MongoDB.Driver;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/settings/db")]
    public class DatabaseController : ControllerBase
    {
        // Backup Database
        [Authorize(Roles = "Admin")]
        [HttpPost("backup")]
        public IActionResult BackupDatabase()
        {
            var success = Transfer.ExportDb();
            if (success)
            {
                return Ok("Database backup completed successfully.");
            }
            else
            {
                return BadRequest("Failed to backup database.");
            }
        }

        // View Backups
        [Authorize(Roles = "Admin")]
        [HttpGet("view-backups")]
        public IActionResult ViewBackups()
        {
            var backupFiles = Directory.GetDirectories($"{StateManager.melonPath}/Exports/DbBackups")
                .Select(x=>x.Split(new string[] {"/", "\\"}, StringSplitOptions.None).Last())
                .ToList();
            return Ok(backupFiles);
        }

        // Load from Backup
        [Authorize(Roles = "Admin")]
        [HttpPost("load-backup")]
        public IActionResult LoadFromBackup(string backupFileName)
        {
            var backupFilePath = Path.Combine($"{StateManager.melonPath}/Exports/DbBackups", backupFileName);
            if (!System.IO.Directory.Exists(backupFilePath))
            {
                return NotFound("Backup file not found.");
            }
            Transfer.ImportDb(backupFileName);
            return Ok("Database loaded from backup successfully.");
        }

        // Reset Database
        [Authorize(Roles = "Admin")]
        [HttpPost("reset")]
        public IActionResult ResetDatabase()
        {
            MelonScanner.ResetDb();
            return Ok("Database reset successfully.");
        }

        // Export Playlist
        [Authorize(Roles = "Admin, User")]
        [HttpGet("export-playlist")]
        public IActionResult ExportPlaylist(string format, string playlistId)
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var PlaylistsCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
            var CollectionsCollection = NewMelonDB.GetCollection<Collection>("Collections");

            var plst = PlaylistsCollection.AsQueryable().Where(x => x._id == playlistId).FirstOrDefault();
            if (plst == null)
            {
                var col = CollectionsCollection.AsQueryable().Where(x => x._id == playlistId).FirstOrDefault();
                if (col != null)
                {
                    plst = new Playlist();
                    plst.Name = col.Name;
                    plst.Tracks = col.Tracks;
                }
            }

            if(plst == null)
            {
                return NotFound("Playlist not found.");
            }

            string filePath = "";
            var check = Transfer.ExportPlaylist(format, plst, out filePath);
            if (!check)
            {
                return BadRequest("Failed to export playlist.");
            }

            try
            {
                var fileStream = new FileStream(filePath, FileMode.Open);
                return File(fileStream, "application/octet-stream", $"{Path.GetFileName(filePath)}");
            }
            catch(Exception)
            {
                return BadRequest("Failed to export playlist.");
            }
        }

        // Import Playlist
        [Authorize(Roles = "Admin, User")]
        [HttpPost("import-playlist")]
        public IActionResult ImportPlaylist([FromForm] IFormFile plstFile)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            if (plstFile == null)
            {
                return BadRequest("No file uploaded.");
            }

            var tempFilePath = $"{Path.GetTempPath()}/{plstFile.FileName.Split(new string[] {"/", "\\"}, StringSplitOptions.None).Last()}";

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                plstFile.CopyTo(stream);
            }

            var check = Transfer.ImportPlaylist(tempFilePath, curId);
            if (!check)
            {
                return BadRequest("File Not Supported.");
            }
            System.IO.File.Delete(tempFilePath);
            return Ok("Playlist imported successfully.");
        }

        // Get Queue Cleanup Frequency
        [Authorize(Roles = "Admin")]
        [HttpGet("queue-cleanup-frequency")]
        public IActionResult GetQueueCleanupFrequency()
        {
            return Ok(StateManager.MelonSettings.QueueCleanupWaitInHours);
        }

        // Set Queue Cleanup Frequency
        [Authorize(Roles = "Admin")]
        [HttpPost("queue-cleanup-frequency")]
        public IActionResult SetQueueCleanupFrequency(double frequencyInHours)
        {
            double cur = StateManager.MelonSettings.QueueCleanupWaitInHours;
            StateManager.MelonSettings.QueueCleanupWaitInHours = frequencyInHours;
            if (cur == -1)
            {
                QueuesCleaner.StartCleaner();
            }
            Storage.SaveConfigFile<Settings>("MelonSettings", StateManager.MelonSettings, null);
            return Ok("Queue cleanup frequency updated successfully.");
        }
    }
}
