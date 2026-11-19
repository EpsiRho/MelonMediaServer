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
            try { return Ok(await ServerAdministration.Instance.CheckForUpdates()); }
            catch (HttpRequestException) { return BadRequest("Failed to check for updates."); }
        }
        // Check For Updates
        [Authorize(Roles = "Admin")]
        [HttpPost("update-melon")]
        public async Task<IActionResult> Update()
        {
            try { return await ServerAdministration.Instance.ApplyUpdate() ? Ok() : Ok("Melon is up to date"); }
            catch (HttpRequestException) { return BadRequest("Failed to check for updates."); }
        }
    }
}
