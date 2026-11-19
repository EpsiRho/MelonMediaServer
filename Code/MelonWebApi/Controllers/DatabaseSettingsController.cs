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
            var success = ServerAdministration.Instance.BackupDatabase();
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
            var backupFiles = ServerAdministration.Instance.ListBackups();
            return Ok(backupFiles);
        }

        // Load from Backup
        [Authorize(Roles = "Admin")]
        [HttpPost("load-backup")]
        public IActionResult LoadFromBackup(string backupFileName)
        {
            try { ServerAdministration.Instance.RestoreBackup(backupFileName); }
            catch (FileNotFoundException) { return NotFound("Backup file not found."); }
            return Ok("Database loaded from backup successfully.");
        }

        // Reset Database
        [Authorize(Roles = "Admin")]
        [HttpPost("reset")]
        public IActionResult ResetDatabase()
        {
            ServerAdministration.Instance.ResetDatabase();
            return Ok("Database reset successfully.");
        }

        // Export Playlist
        [Authorize(Roles = "Admin, User")]
        [HttpGet("export-playlist")]
        public IActionResult ExportPlaylist(string format, string playlistId)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            try
            {
                if (string.IsNullOrEmpty(curId)) return Forbid();
                var export = Transfer.GetPlaylistExport(playlistId, format, curId);
                return File(System.Text.Encoding.UTF8.GetBytes(export.Contents), "application/octet-stream", export.Name);
            }
            catch (FileNotFoundException) { return NotFound("Playlist not found."); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
        }

        // Import Playlist
        [Authorize(Roles = "Admin, User")]
        [HttpPost("import-playlist")]
        public IActionResult ImportPlaylist(IFormFile plstFile)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                      .Where(c => c.Type == ClaimTypes.UserData)
                      .Select(c => c.Value).FirstOrDefault();
            if (plstFile == null)
            {
                return BadRequest("No file uploaded.");
            }

            if (string.IsNullOrEmpty(curId)) return Forbid();
            var extension = Path.GetExtension(Path.GetFileName(plstFile.FileName));
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.CreateNew)) plstFile.CopyTo(stream);
                return ServerAdministration.Instance.ImportPlaylists(tempFilePath, curId) == 1
                    ? Ok("Playlist imported successfully.") : BadRequest("File Not Supported.");
            }
            finally { System.IO.File.Delete(tempFilePath); }
        }

        // Get Queue Cleanup Frequency
        [Authorize(Roles = "Admin")]
        [HttpGet("queue-cleanup-frequency")]
        public IActionResult GetQueueCleanupFrequency()
        {
            return Ok(ServerAdministration.Instance.GetSettings().QueueCleanupHours);
        }

        // Set Queue Cleanup Frequency
        [Authorize(Roles = "Admin")]
        [HttpPost("queue-cleanup-frequency")]
        public IActionResult SetQueueCleanupFrequency(double frequencyInHours)
        {
            try { ServerAdministration.Instance.SetQueueCleanupHours(frequencyInHours); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            return Ok("Queue cleanup frequency updated successfully.");
        }
    }
}
