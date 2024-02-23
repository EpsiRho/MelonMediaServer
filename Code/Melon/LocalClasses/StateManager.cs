using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melon.Classes;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using System.Text.Json;
using Melon.DisplayClasses;
using Pastel;
using System.Drawing;
using Newtonsoft.Json;
using System.Diagnostics;
using Amazon.Util;
using Microsoft.Extensions.DependencyInjection;
using System.Resources;
using Amazon.Util.Internal;
using Microsoft.IdentityModel.Tokens;
using Melon.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Reflection;
using System.Windows.Input;
using Melon.Interface;
using Melon.PluginModels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Melon.LocalClasses
{
    /// <summary>
    /// Handles melon's state, such as settings.
    /// </summary>
    public static class StateManager
    {
        public static string melonPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/Melon";
        public static MongoClient DbClient;
        public static Settings MelonSettings { get; set; }
        public static Flags MelonFlags { get; set; }
        public static ResourceManager StringsManager { get; set; }
        public static List<IPlugin> Plugins { get; set; }
        public static List<PluginLoadContext> PluginsContexts { get; set; }
        public static List<string> DisabledPlugins { get; set; }
        public static MelonHost Host { get; set; }
        private static void LoadSettings()
        {
            if (!File.Exists($"{melonPath}/Configs/MelonSettings.json"))
            {
                // Default settings if settings don't exist
                MelonSettings = new Settings()
                {
                    MongoDbConnectionString = "mongodb://localhost:27017",
                    LibraryPaths = new List<string>(),
                    JWTKey = Security.GenerateSecretKey(),
                    Text = Color.FromArgb(204, 204, 204),
                    ShadedText = Color.FromArgb(100, 100, 100),
                    BackgroundText = Color.FromArgb(66, 66, 66),
                    Highlight = Color.FromArgb(97, 214, 214),
                    Melon = Color.FromArgb(26, 225, 19),
                    Error = Color.FromArgb(255, 0, 0),
                    ListeningURL = "https://*:14524",
                    DefaultLanguage = "EN",
                    JWTExpireInMinutes = 60,
                    UseMenuColor = true,
                };
                MelonColor.SetDefaults();
                DisplayManager.UIExtensions.Add(SetupUI.Display); // Add SetupUI to UIExtentions so we can show the OOBE
                Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
            }
            else
            {
                // Load settings
                MelonSettings = Storage.LoadConfigFile<Settings>("MelonSettings", new[] { "JWTKey" });
                
                if(MelonSettings == null)
                {
                    MelonSettings = new Settings()
                    {
                        MongoDbConnectionString = "mongodb://localhost:27017",
                        LibraryPaths = new List<string>(),
                        JWTKey = Security.GenerateSecretKey(),
                        Text = Color.FromArgb(204, 204, 204),
                        ShadedText = Color.FromArgb(100, 100, 100),
                        BackgroundText = Color.FromArgb(66, 66, 66),
                        Highlight = Color.FromArgb(97, 214, 214),
                        Melon = Color.FromArgb(26, 225, 19),
                        Error = Color.FromArgb(255, 0, 0),
                        ListeningURL = "https://*:14524",
                        DefaultLanguage = "EN",
                        JWTExpireInMinutes = 60,
                        UseMenuColor = true,
                    };
                }
                
                if (MelonSettings.DefaultLanguage.IsNullOrEmpty())
                {
                    MelonSettings.DefaultLanguage = "EN";
                }

                if(MelonSettings.JWTKey.IsNullOrEmpty())
                {
                    MelonSettings.JWTKey = Security.GenerateSecretKey();
                }

                if(MelonSettings.ListeningURL.IsNullOrEmpty())
                {
                    MelonSettings.ListeningURL = "https://*:14524";
                }

                // Set Colors
                MelonColor.Text = MelonSettings.Text;
                MelonColor.ShadedText = MelonSettings.ShadedText;
                MelonColor.BackgroundText = MelonSettings.BackgroundText;
                MelonColor.Highlight = MelonSettings.Highlight;
                MelonColor.Melon = MelonSettings.Melon;
                MelonColor.Error = MelonSettings.Error;
            }
        }
        private static void SetLanguage(string language)
        {
            if (language == "")
            {
                language = MelonSettings.DefaultLanguage;
            }

            var resources = typeof(Program).Assembly.GetManifestResourceNames();
            if (resources.Contains($"Melon.Strings.UIStrings{language.ToUpper()}.resources"))
            {
                StringsManager = new ResourceManager($"Melon.Strings.UIStrings{language.ToUpper()}", typeof(Program).Assembly);
            }
            else
            {
                StringsManager = new ResourceManager($"Melon.Strings.UIStringsEN", typeof(Program).Assembly);
            }
        }
        private static void CreateDirectories()
        {
            Directory.CreateDirectory($"{melonPath}/Configs");
            Directory.CreateDirectory($"{melonPath}/AlbumArts");
            Directory.CreateDirectory($"{melonPath}/ArtistPfps");
            Directory.CreateDirectory($"{melonPath}/ArtistBanners");
            Directory.CreateDirectory($"{melonPath}/Assets");
            Directory.CreateDirectory($"{melonPath}/CollectionArts");
            Directory.CreateDirectory($"{melonPath}/PlaylistArts");
            Directory.CreateDirectory($"{melonPath}/Plugins");
        }
        private static void LoadFlags()
        {
            if (!File.Exists($"{melonPath}/Configs/MelonFlags.json"))
            {
                MelonFlags = new Flags
                {
                    DisablePlugins = false,
                    ForceOOBE = false
                };
                Storage.SaveConfigFile<Flags>("MelonFlags", MelonFlags, null);
            }
            else
            {
                MelonFlags = Storage.LoadConfigFile<Flags>("MelonFlags", null);
                if (MelonFlags == null)
                {
                    MelonFlags = new Flags
                    {
                        DisablePlugins = false,
                        ForceOOBE = false
                    };
                }
            }
        }
        public static void Init(bool headless, bool runSetup, bool loadPlugins, string language, IWebApi mWebApi)
        {
            // Title
            MelonColor.SetDefaults();   
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Init" });
            if (!headless)
            {
                // Setup checklist UI
                ChecklistUI.SetChecklistItems(new[]
                {
                    "Load settings",
                    "Connect to MongoDB",
                    "Load Plugins"
                });
                ChecklistUI.ChecklistDislayToggle();
            }

            Host = new MelonHost() { WebApi = mWebApi };

            CreateDirectories();
            LoadSettings();
            SetLanguage(language);

            // Reload UI in set language
            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
            if (!headless)
            {
                // Setup checklist UI
                ChecklistUI.SetChecklistItems(new[]
                {
                    StringsManager.GetString("SettingsLoadStatus"),
                    StringsManager.GetString("MongoDBConnectStatus"),
                    StringsManager.GetString("LoadPluginsStatus")
                });
            }

            LoadFlags();

            // Show OOBE if flag/arg enabled and headless disabled
            if ((MelonFlags.ForceOOBE || runSetup) && !headless)
            {
                DisplayManager.UIExtensions.Add(SetupUI.Display);
            }

            // Load SSLConfig if exists
            var config = Storage.LoadConfigFile<SSLConfig>("SSLConfig", new[] { "Password" });
            if (config != null)
            {
                Security.SetSSLConfig(config);
            }
            else
            {
                Security.SetSSLConfig("", "");
            }

            // Update the checklistUI, Load Settings done
            if (!headless)
            {
                ChecklistUI.UpdateChecklist(0, true);
            }

            // Setup MongoDb
            var connectionString = MelonSettings.MongoDbConnectionString;
            var check = CheckMongoDB(connectionString);
            if (!check && !headless)
            {
                // MongoDb connection failed
                ChecklistUI.ChecklistDislayToggle();
                Thread.Sleep(200);

                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Init" });
                Console.WriteLine(StringsManager.GetString("MongoDBConnectionError").Pastel(MelonColor.Error));

                if (!headless)
                {
                    Console.WriteLine(StringsManager.GetString("ReturnPrompt").Pastel(MelonColor.BackgroundText));
                    Console.ReadKey(intercept: true);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));
                    return;
                }
                else
                {
                    Environment.Exit(1);
                }
            }

            // Setup Display Options
            DisplayManager.MenuOptions.Add(StringsManager.GetString("FullScanOption"), MelonScanner.Scan);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("ShortScanOption"), MelonScanner.ScanShort);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("DatabaseResetConfirmation"), MelonScanner.ResetDBUI);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));

            if (!headless)
            {
                ChecklistUI.UpdateChecklist(1, true);
            }

            // Plugins
            if (!MelonFlags.DisablePlugins || loadPlugins)
            {
                if (File.Exists($"{melonPath}/Configs/DisabledPlugins.json"))
                {
                    DisabledPlugins = Storage.LoadConfigFile<List<string>>("DisabledPlugins.json", null);
                }
                else
                {
                    DisabledPlugins = new List<string>();
                    Storage.SaveConfigFile("DisabledPlugins.json", DisabledPlugins, null);
                }
                PluginsContexts = new List<PluginLoadContext>();

                LoadPlugins();

            }

            if (!headless)
            {
                ChecklistUI.UpdateChecklist(2, true);
            }

            if (!headless)
            {
                ChecklistUI.ChecklistDislayToggle();
                Thread.Sleep(200);
            }
            

        }
        public static bool CheckMongoDB(string connectionString)
        {
            int count = 0;
            while (count < 10)
            {
                try
                {
                    MongoClientSettings settings = MongoClientSettings.FromConnectionString(connectionString);
                    settings.ConnectTimeout = TimeSpan.FromSeconds(10);
                    DbClient = new MongoClient(settings);
                    if (DbClient.Cluster.Description.State == MongoDB.Driver.Core.Clusters.ClusterState.Connected)
                    {
                        return true;
                    }
                    else
                    {
                        count++;
                        Thread.Sleep(100);
                    }
                }
                catch (Exception)
                {
                    count++;
                }
            }
            return false;
        }
        public static byte[] GetDefaultImage()
        {
            var filePath = $"{StateManager.melonPath}/Assets/defaultArtwork.jpg";
            if (File.Exists(filePath))
            {
                FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using (MemoryStream ms = new MemoryStream())
                {
                    file.CopyTo(ms);
                    file.Close();
                    return ms.ToArray();
                }
            }

            var stream = typeof(Program).Assembly.GetManifestResourceStream("Melon.Assets.defaultArtwork.png");
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        public static void LoadPlugins()
        {
            if (!Directory.Exists($"{melonPath}/Plugins"))
            {
                Directory.CreateDirectory($"{melonPath}/Plugins");
            }

            var files = Directory.GetFiles($"{melonPath}/Plugins");
            Plugins = new List<IPlugin>();
            foreach(var file in files)
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
                plugin.LoadMelonCommands(Host);
                var check = plugin.Load();
                if(check != 0)
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
