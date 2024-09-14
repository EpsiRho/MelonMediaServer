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
            var pluginsInfo = StateManager.Plugins.Select(p => new
            {
                p.Name,
                p.Authors,
                p.Version,
                p.Description,
                IsEnabled = !StateManager.DisabledPlugins.Contains($"{p.Name}:{p.Authors}")
            });
            return Ok(pluginsInfo);
        }

        // Disable Plugin
        [Authorize(Roles = "Admin")]
        [HttpPost("disable")]
        public IActionResult DisablePlugin(string name, string author)
        {
            var plugin = StateManager.Plugins.FirstOrDefault(p => p.Name == name && p.Authors == author);
            if (plugin == null)
            {
                return NotFound("Plugin not found.");
            }
            //plugin.UnloadUI();
            plugin.Destroy();
            StateManager.DisabledPlugins.Add($"{plugin.Name}:{plugin.Authors}");
            Storage.SaveConfigFile("DisabledPlugins", StateManager.DisabledPlugins, null);
            return Ok("Plugin disabled successfully.");
        }

        // Enable Plugin
        [Authorize(Roles = "Admin")]
        [HttpPost("enable")]
        public IActionResult EnablePlugin(string name, string author)
        {
            var plugin = StateManager.Plugins.FirstOrDefault(p => p.Name == name && p.Authors == author);
            if (plugin == null)
            {
                return NotFound("Plugin not found.");
            }
            try
            {
                plugin.LoadMelonCommands(StateManager.Host);
                plugin.LoadMelonServerCommands(StateManager.WebApi);
                plugin.Execute();
                StateManager.DisabledPlugins.Remove($"{plugin.Name}:{plugin.Authors}");
                Storage.SaveConfigFile("DisabledPlugins", StateManager.DisabledPlugins, null);
                return Ok("Plugin enabled successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error enabling plugin: {ex.Message}");
            }
        }

        // Rescan Plugins Folder
        [Authorize(Roles = "Admin")]
        [HttpPost("rescan")]
        public IActionResult RescanPlugins()
        {
            try
            {
                //Storage.SaveConfigFile("DisabledPlugins", StateManager.DisabledPlugins, null);
                //System.IO.File.WriteAllText($"{StateManager.melonPath}/Configs/restartServer.json", "1");
                foreach (var plugin in StateManager.Plugins)
                {
                    plugin.Destroy();
                }
                PluginsManager.LoadPlugins();
                PluginsManager.ExecutePlugins();
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
                //Storage.SaveConfigFile("DisabledPlugins", StateManager.DisabledPlugins, null);
                //System.IO.File.WriteAllText($"{StateManager.melonPath}/Configs/restartServer.json", "1");
                foreach (var plugin in StateManager.Plugins)
                {
                    plugin.Destroy();
                }
                foreach (var plugin in StateManager.Plugins)
                {
                    plugin.LoadMelonCommands(StateManager.Host);
                    plugin.LoadMelonServerCommands(StateManager.WebApi);
                    plugin.Execute();
                }
                return Ok("Plugins reloaded successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error reloading plugins: {ex.Message}");
            }
        }
    }
}
