using Melon.Interface;
using Melon.Models;
using MelonTestPlugin.Models;
using System.Text.Json;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Windows.Input;

namespace MelonPlugin
{
    public class EventPlugin : IPlugin, IControllerPlugin
    {
        public string Name => "Demo Plugin";
        public string Authors => "Epsi";
        public string Version => "v1.4.0";
        public string Description => "Demo plugin";
        public IHost Host { get; set; }
        public IWebApi WebApi { get; set; }
        public EventConfig Config;
  
        public byte[] EventsInfoMiddleware(WebApiEventArgs args)
        {
            if (args.Api == "/api/demo/info")
            {
                var result = $"{Name}\n{Authors}\n{Version}\n{Description}";
                return Encoding.ASCII.GetBytes(result);
            }

            return null;
        }


        private void LoadConfig()
        {
            Config = Host.Storage.LoadConfigFile<EventConfig>("DemoConfig", null, out _);

            if(Config == null)
            {
                Config = new EventConfig()
                {
                    Format = "flac",
                    TextColor = Color.FromArgb(255, 255, 255, 255),
                    ShowArgs = false
                };
                Host.Storage.SaveConfigFile("DemoConfig", Config, null);
            }
        }

        public PluginPage GetPage() => new(
            [new("format", "Format", "text", Config.Format), new("textColor", "Text color", "argb", Config.TextColor.ToArgb()),
             new("showArgs", "Show arguments", "boolean", Config.ShowArgs)],
            [new("save", "Save settings"), new("bark", "Bark"), new("meow", "Meow")]);

        public object? Invoke(string action, JsonElement values)
        {
            if (action == "bark") return "Awruff!";
            if (action == "meow") return "Mrrow!";
            if (action != "save") throw new ArgumentException("Unknown demo action.");
            var config = new EventConfig
            {
                Format = values.GetProperty("format").GetString() ?? throw new ArgumentException("Missing format."),
                TextColor = Color.FromArgb(values.GetProperty("textColor").GetInt32()),
                ShowArgs = values.GetProperty("showArgs").GetBoolean()
            };
            Host.Storage.SaveConfigFile("DemoConfig", config, null);
            Config = config;
            return GetPage();
        }

        public void LoadMelonCommands(IHost host)
        {
            Host = host;
        }

        public Dictionary<string, string> GetHelpOptions()
        {
            return new Dictionary<string, string>()
            {
                { "--resetDemoSettings", "Resets the Demo plugin's settings to their defaults." }
            };
        }

        public void LoadMelonServerCommands(IWebApi webapi)
        {
            WebApi = webapi;
        }

        public int Execute()
        {
            if (Host.StateManager.LaunchArgs.ContainsKey("resetDemoSettings"))
            {
                Config = new EventConfig()
                {
                    Format = "flac",
                    TextColor = Color.FromArgb(255, 255, 255, 255),
                    ShowArgs = false
                };
                Host.Storage.SaveConfigFile("DemoConfig", Config, null);
            }
            else
            {

                LoadConfig();
            }
            WebApi.UsePluginMiddleware(KeyValuePair.Create("DemoMiddleware", EventsInfoMiddleware));

            return 0;
        }

        public int Destroy()
        {
            WebApi.RemovePluginMiddleware("DemoMiddleware");
            return 0;
        }
    }
}