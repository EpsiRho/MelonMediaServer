using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/settings/plugins")]
    public class PluginsController : ControllerBase
    {
        // View Plugins
        [Authorize(Roles = "Admin")]
        [HttpGet("list")]
        public IActionResult GetPlugins()
        {
            return Ok(ServerAdministration.Instance.GetPlugins());
        }

        // Disable Plugin
        [Authorize(Roles = "Admin")]
        [HttpPost("disable")]
        public IActionResult DisablePlugin(string name, string author)
        {
            return ServerAdministration.Instance.SetPluginEnabled(name, author, false)
                ? Ok("Plugin disabled successfully.") : NotFound("Plugin not found.");
        }

        // Enable Plugin
        [Authorize(Roles = "Admin")]
        [HttpPost("enable")]
        public IActionResult EnablePlugin(string name, string author)
        {
            return ServerAdministration.Instance.SetPluginEnabled(name, author, true)
                ? Ok("Plugin enabled successfully.") : NotFound("Plugin not found.");
        }

        // Rescan Plugins Folder
        [Authorize(Roles = "Admin")]
        [HttpPost("rescan")]
        public IActionResult RescanPlugins()
        {
            try
            {
                ServerAdministration.Instance.ReloadPlugins(true);
                return Ok("Plugins folder rescanned successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error rescanning plugins: {ex.Message}");
            }
        }

        // Reload Plugins
        [Authorize(Roles = "Admin")]
        [HttpPost("reload")]
        public IActionResult ReloadPlugins()
        {
            try
            {
                ServerAdministration.Instance.ReloadPlugins(false);
                return Ok("Plugins reloaded successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error reloading plugins: {ex.Message}");
            }
        }
    }
}
