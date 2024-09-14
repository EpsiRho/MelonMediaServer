using Melon.LocalClasses;
using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/settings/scanner")]
    public class MelonScannerSettingsController : ControllerBase
    {
        // View Library Paths
        [Authorize(Roles = "Admin")]
        [HttpGet("library-paths")]
        public IActionResult GetLibraryPaths()
        {
            var libraryPaths = StateManager.MelonSettings.LibraryPaths;
            return Ok(libraryPaths);
        }

        // Set Library Paths
        [Authorize(Roles = "Admin")]
        [HttpPost("library-paths")]
        public IActionResult SetLibraryPaths([FromBody] List<string> newLibraryPaths)
        {
            foreach (var path in newLibraryPaths)
            {
                if (!Directory.Exists(path))
                {
                    return BadRequest($"Path does not exist: {path}");
                }
            }
            StateManager.MelonSettings.LibraryPaths = newLibraryPaths;
            Storage.SaveConfigFile<Settings>("MelonSettings", StateManager.MelonSettings, null);
            return Ok("Library paths updated successfully.");
        }

        // View Artist Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpGet("artist-split-indicators")]
        public IActionResult GetArtistSplitIndicators()
        {
            return Ok(StateManager.MelonSettings.ArtistSplitIndicators);
        }

        // Set Artist Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-split-indicators")]
        public IActionResult SetArtistSplitIndicators([FromBody] List<string> indicators)
        {
            StateManager.MelonSettings.ArtistSplitIndicators = indicators;
            Storage.SaveConfigFile<Settings>("MelonSettings", StateManager.MelonSettings, null);
            return Ok("Artist split indicators updated successfully.");
        }

        // View Genre Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpGet("genre-split-indicators")]
        public IActionResult GetGenreSplitIndicators()
        {
            return Ok(StateManager.MelonSettings.GenreSplitIndicators);
        }

        // Set Genre Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpPost("genre-split-indicators")]
        public IActionResult SetGenreSplitIndicators([FromBody] List<string> indicators)
        {
            StateManager.MelonSettings.GenreSplitIndicators = indicators;
            Storage.SaveConfigFile<Settings>("MelonSettings", StateManager.MelonSettings, null);
            return Ok("Genre split indicators updated successfully.");
        }
    }
}
