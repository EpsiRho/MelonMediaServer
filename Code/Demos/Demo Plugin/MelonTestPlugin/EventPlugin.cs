using Melon.Interface;
using Melon.Models;
using MelonTestPlugin.Models;
using Pastel;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Windows.Input;

namespace MelonPlugin
{
    public class EventPlugin : IPlugin
    {
        public string Name => "Demo Plugin";
        public string Authors => "Epsi";
        public string Version => "v1.3.0";
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
                    Format = "[api] (userId): msg",
                    TextColor = Color.FromArgb(255, 255, 255, 255),
                    ShowArgs = false
                };
                Host.Storage.SaveConfigFile("DemoConfig", Config, null);
            }
        }

        public void SettingsMenu()
        {
            while (true)
            {
                Host.MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Demo Plugin Settings" });
                string args = Config.ShowArgs ? "Hide Args (Currently Shown)" : "Show Args (Currently Hidden)";
                var choice = Host.MelonUI.OptionPicker(new List<string>()
                {
                    "Back",
                    "Change Format",
                    "Change Color",
                    args
                });

                switch (choice)
                {
                    case "Back":
                        return;
                    case "Change Format":
                        ChangeFormatMenu();
                        break;
                    case "Change Color":
                        ChangeColorMenu();
                        break;
                    case "Hide Args (Currently Shown)":
                        Config.ShowArgs = !Config.ShowArgs;
                        Host.Storage.SaveConfigFile("DemoConfig", Config, null);
                        break;
                    case "Show Args (Currently Hidden)":
                        Config.ShowArgs = !Config.ShowArgs;
                        Host.Storage.SaveConfigFile("DemoConfig", Config, null);
                        break;
                }
            }
        }
        public void ChangeFormatMenu()
        {
            Host.MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Demo Plugin Settings", "Change Format" });
            Console.WriteLine("Format markers: api, statuscode, msg, userId".Pastel(Config.TextColor));
            Console.WriteLine($"Current format: {Config.Format}".Pastel(Config.TextColor));
            Console.WriteLine($"(Enter nothing to go back)".Pastel(Config.TextColor));
            Console.Write("> ");
            var input = Console.ReadLine();
            if(input == "")
            {
                return;
            }

            Config.Format = input;
            Host.Storage.SaveConfigFile("DemoConfig", Config, null);
        }
        public void ChangeColorMenu()
        {
            Host.MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Demo Plugin Settings", "Change Color" });
            var newColor = Host.MelonUI.ColorPicker(Config.TextColor);
            Config.TextColor = newColor;
            Host.Storage.SaveConfigFile("DemoConfig", Config, null);
        }

        private void EventMenu()
        {
            Host.MelonUI.BreadCrumbBar(new List<string>(){"Melon", "Demo Plugin"});
            Console.WriteLine($"Please answer the following question:");
            var choice = Host.MelonUI.OptionPicker(new List<string>() { "Bark", "Meow" });
            if(choice == "Bark")
            {
                Console.WriteLine("Awruff!");
            }
            else
            {
                Console.WriteLine("Mrrow!");
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
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

        public int LoadUI()
        {
            if (Host.StateManager.LaunchArgs.ContainsKey("resetDemoSettings"))
            {
                Config = new EventConfig()
                {
                    Format = "[api] (userId): msg",
                    TextColor = Color.FromArgb(255, 255, 255, 255),
                    ShowArgs = false
                };
                Host.Storage.SaveConfigFile("DemoConfig", Config, null);
            }
            else
            {

                LoadConfig();
            }
            Host.DisplayManager.MenuOptions.Insert(Host.DisplayManager.MenuOptions.Count - 1, "Plugin Demo", EventMenu);
            Host.SettingsUI.MenuOptions.Add("Demo Settings", SettingsMenu);
            return 0;
        }

        public int UnloadUI()
        {
            Host.DisplayManager.MenuOptions.Remove("Plugin Demo");
            Host.SettingsUI.MenuOptions.Remove("Demo Settings");
            return 0;
        }

        public int Execute()
        {
            if (Host.StateManager.LaunchArgs.ContainsKey("resetDemoSettings"))
            {
                Config = new EventConfig()
                {
                    Format = "[api] (userId): msg",
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