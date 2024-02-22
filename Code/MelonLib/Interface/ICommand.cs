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
        /// <summary>
        /// Loads the Host from the Melon Server that gives access to the plugin api.
        /// </summary>
        /// <param name="host">The MelonHost</param>
        public void LoadMelonCommands(IHost host);

        /// <summary>
        /// Called on startup, this should be where your plugin does it's setup, adds any ui, and starts any processes it needs.
        /// </summary>
        /// <returns>int indicating success or failure result</returns>
        public int Load();

        /// <summary>
        /// Called when reloading or disabling a plugin, this should remove any ui and close extra processes.
        /// </summary>
        /// <returns>int indicating success or failure result</returns>
        public int Unload();
    }
}
