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
            var libraryPaths = ServerAdministration.Instance.GetSettings().LibraryPaths;
            return Ok(libraryPaths);
        }

        // Set Library Paths
        [Authorize(Roles = "Admin")]
        [HttpPost("library-paths")]
        public IActionResult SetLibraryPaths([FromBody] List<string> newLibraryPaths)
        {
            try { ServerAdministration.Instance.SetLibraryPaths(newLibraryPaths.ToArray()); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            return Ok("Library paths updated successfully.");
        }

        // View Artist Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpGet("artist-split-indicators")]
        public IActionResult GetArtistSplitIndicators()
        {
            return Ok(ServerAdministration.Instance.GetSettings().ArtistSplitIndicators);
        }

        // Set Artist Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpPost("artist-split-indicators")]
        public IActionResult SetArtistSplitIndicators([FromBody] List<string> indicators)
        {
            try { ServerAdministration.Instance.SetArtistSplitIndicators(indicators.ToArray()); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            return Ok("Artist split indicators updated successfully.");
        }

        // View Genre Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpGet("genre-split-indicators")]
        public IActionResult GetGenreSplitIndicators()
        {
            return Ok(ServerAdministration.Instance.GetSettings().GenreSplitIndicators);
        }

        // Set Genre Split Indicators
        [Authorize(Roles = "Admin")]
        [HttpPost("genre-split-indicators")]
        public IActionResult SetGenreSplitIndicators([FromBody] List<string> indicators)
        {
            try { ServerAdministration.Instance.SetGenreSplitIndicators(indicators.ToArray()); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            return Ok("Genre split indicators updated successfully.");
        }
    }
}
