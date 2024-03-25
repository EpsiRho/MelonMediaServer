using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Interface
{
    public interface IPlugin
    {
        /// <summary>
        /// The name of the plugin
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The plugin version.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The author or authors of a plugin, separated by commas.
        /// </summary>
        public string Authors { get; }

        /// <summary>
        /// A short description about the plugin
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The plugin api host
        /// </summary>
        public IHost Host { get; set; }
        public IWebApi WebApi { get; set; }

        /// <summary>
        /// The plugin api host
        /// </summary>
        public Dictionary<string, string> GetHelpOptions();

        /// <summary>
        /// Loads the Host from the Melon UI that gives access to many of the plugin APIs.
        /// </summary>
        /// <param name="host">The MelonHost</param>
        public void LoadMelonCommands(IHost host);

        /// <summary>
        /// Loads the Host from the Melon Server that gives access to the server plugin APIs.
        /// </summary>
        /// <param name="host">The MelonHost</param>
        public void LoadMelonServerCommands(IWebApi webapi);

        /// <summary>
        /// Called on startup, this should be where your plugin adds any ui it needs.
        /// </summary>
        /// <returns>int indicating success or failure result</returns>
        public int LoadUI();

        /// <summary>
        /// Called when reloading or disabling a plugin, this should remove any ui.
        /// </summary>
        /// <returns>int indicating success or failure result</returns>
        public int UnloadUI();

        /// <summary>
        /// Called on startup, this should include any processes or middleware you want to add that should be running until melon closes or your plugin is disabled.
        /// </summary>
        /// <returns>int indicating success or failure result</returns>
        public int Execute();

        /// <summary>
        /// Called when reloading or disabling a plugin, this close any processes or middleware you added.
        /// </summary>
        /// <returns>int indicating success or failure result</returns>
        public int Destroy();
    }
}
