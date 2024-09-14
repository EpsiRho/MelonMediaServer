using Melon.DisplayClasses;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/settings")]
    public class CheckForUpdateUpdateController : ControllerBase
    {
        // Check For Updates
        [Authorize(Roles = "Admin, User")]
        [HttpGet("check-for-updates")]
        public async Task<IActionResult> CheckForUpdates()
        {
            var release = await SettingsUI.GetGithubRelease("latest");
            if (release != null)
            {
                var curVersion = System.Version.Parse(StateManager.Version);
                var latestVersion = System.Version.Parse(release.tag_name.Replace("v", ""));
                if (curVersion >= latestVersion)
                {
                    return Ok(new
                    {
                        UpdateAvailable = false,
                        CurrentVersion = StateManager.Version,
                        LatestVersion = StateManager.Version,
                        ReleaseNotes = ""
                    });
                }
                else
                {
                    return Ok(new
                    {
                        UpdateAvailable = true,
                        CurrentVersion = StateManager.Version,
                        LatestVersion = release.tag_name,
                        ReleaseNotes = release.body
                    });
                }
            }
            else
            {
                return BadRequest("Failed to check for updates.");
            }
        }
        // Check For Updates
        [Authorize(Roles = "Admin")]
        [HttpPost("update-melon")]
        public async Task<IActionResult> Update()
        {
            var release = SettingsUI.GetGithubRelease("latest").Result;
            if (release != null)
            {
                var curVersion = System.Version.Parse(StateManager.Version);
                var latestVersion = System.Version.Parse(release.tag_name.Replace("v", ""));
                if (curVersion >= latestVersion)
                {
                    return Ok("Melon is up to date");
                }

                try
                {
                    var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MelonInstaller.exe");
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = $"-update -restart -installPath {AppDomain.CurrentDomain.BaseDirectory} -lang {StateManager.Language}",
                        UseShellExecute = false
                    };
                    Process.Start(processInfo);

                    Environment.Exit(0);

                    return Ok();
                }
                catch (Exception)
                {
                    return BadRequest();
                }
            }
            else
            {
                return NotFound("No update found!");
            }
        }
    }
}
