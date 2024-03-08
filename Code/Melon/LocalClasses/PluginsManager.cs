using Amazon.Runtime.Internal.Transform;
using Melon.Classes;
using Melon.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Melon.LocalClasses.StateManager;

namespace Melon.LocalClasses
{
    public static class PluginsManager
    {
        public static void LoadPlugins()
        {
            if (!Directory.Exists($"{melonPath}/Plugins"))
            {
                Directory.CreateDirectory($"{melonPath}/Plugins");
            }

            var files = Directory.GetFiles($"{melonPath}/Plugins");
            Plugins = new List<IPlugin>();
            foreach (var file in files)
            {
                try
                {
                    PluginLoadContext context;
                    Assembly pluginAssembly = LoadPlugin(file, out context);
                    Plugins.AddRange(CreatePlugins(pluginAssembly));
                    PluginsContexts.Add(context);
                }
                catch (Exception)
                {

                }
            }
            foreach (var plugin in Plugins)
            {
                if (DisabledPlugins.Contains($"{plugin.Name}:{plugin.Authors}"))
                {
                    continue;
                }
                var options = plugin.GetHelpOptions();
                foreach(var option in options)
                {
                    DisplayManager.HelpOptions.Add(option.Key, option.Value);
                }
            }
        }
        public static void ExecutePlugins()
        {
            foreach (var plugin in Plugins)
            {
                if (DisabledPlugins.Contains($"{plugin.Name}:{plugin.Authors}"))
                {
                    continue;
                }
                plugin.LoadMelonCommands(Host);
                try
                {
                    var check = plugin.Load();
                    if (check != 0)
                    {
                        Serilog.Log.Error($"Plugin Execute failed: {plugin.Name}");
                    }
                }
                catch (Exception)
                {
                    Serilog.Log.Error($"Plugin Execute failed: {plugin.Name}");
                }
            }
        }
        private static Assembly LoadPlugin(string path, out PluginLoadContext context)
        {
            PluginLoadContext loadContext = new PluginLoadContext(path);
            var result = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));
            context = loadContext;
            return result;
        }
        private static List<IPlugin> CreatePlugins(Assembly assembly)
        {
            List<IPlugin> plugins = new List<IPlugin>();
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name.Contains("IPlugin"))
                    {
                        bool check = true;
                    }
                    if (typeof(IPlugin).IsAssignableFrom(type))
                    {
                        IPlugin result = Activator.CreateInstance(type) as IPlugin;
                        if (result != null)
                        {
                            plugins.Add(result);
                        }
                    }
                }
                return plugins;
            }
            catch (Exception)
            {
                Serilog.Log.Error($"Plugin Load failed: {assembly.FullName}");
                return plugins;
            }
        }
    }
}
