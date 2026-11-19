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

            Plugins = new List<IPlugin>();
            foreach (var path in Directory.EnumerateFiles(Path.Combine(melonPath, "Plugins"), "*.dll"))
            {
                var context = new PluginLoadContext(path);
                try
                {
                    var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
                    var types = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type));
                    var discovered = types.Select(type => (IPlugin)Activator.CreateInstance(type)!).ToList();
                    if (discovered.Count == 0) { context.Unload(); continue; }
                    Plugins.AddRange(discovered);
                    PluginsContexts.Add(context);
                }
                catch (Exception ex)
                {
                    context.Unload();
                    Serilog.Log.Warning(ex, "Unable to load plugin {Path}", path);
                }
            }
        }
    }
}
