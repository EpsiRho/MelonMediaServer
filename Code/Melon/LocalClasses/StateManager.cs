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
        public static void Init(bool headless, bool runSetup, bool loadPlugins, string language, IWebApi mWebApi)
        {
            if (language == "")
            {
                try
                {
                    LoadSettings();
                    language = MelonSettings.DefaultLanguage;
                }
                catch (Exception)
                {
                    language = "EN";
                }
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


            // Title
            MelonColor.SetDefaults();
            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
            if (!headless)
            {
                // Setup checklist UI
                ChecklistUI.CreateChecklist(new List<string>()
            {
                StringsManager.GetString("SettingsLoadStatus"),
                StringsManager.GetString("MongoDBConnectStatus"),
                StringsManager.GetString("LoadPluginsStatus")
            });
            }

            if (!headless)
            {
                ChecklistUI.ShowChecklist();
            }


            // Load Settings
            if (!Directory.Exists(melonPath))
            {
                Directory.CreateDirectory(melonPath);
            }

            if (!Directory.Exists($"{StateManager.melonPath}/AlbumArts"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/AlbumArts");
            }
            Security.LoadConnections();

            if (!File.Exists($"{melonPath}/Flags.json"))
            {
                MelonFlags = new Flags()
                {
                    ForceOOBE = false
                };
                SaveFlags();
            }
            else
            {
                LoadFlags();
            }

            if ((MelonFlags.ForceOOBE || runSetup) && !headless)
            {
                DisplayManager.UIExtensions.Add(SetupUI.Display);
            }

            if (!File.Exists($"{melonPath}/Settings.json"))
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
                DisplayManager.UIExtensions.Add(SetupUI.Display);
            }
            else
            {
                try
                {
                    LoadSettings();
                    if(MelonSettings.DefaultLanguage.IsNullOrEmpty())
                    {
                        MelonSettings.DefaultLanguage = "EN";
                    }
                    MelonColor.Text = MelonSettings.Text;
                    MelonColor.ShadedText = MelonSettings.ShadedText;
                    MelonColor.BackgroundText = MelonSettings.BackgroundText;
                    MelonColor.Highlight = MelonSettings.Highlight;
                    MelonColor.Melon = MelonSettings.Melon;
                    MelonColor.Error = MelonSettings.Error;

                }
                catch (Exception)
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
                   
                    DisplayManager.UIExtensions.Add(SetupUI.Display);
                }

            }

            if (File.Exists($"{melonPath}/SSLConfig.json"))
            {
                Security.LoadSSLConfig();
            }
            else
            {
                Security.SetSSLConfig("", "");
            }

            if (!headless)
            {
                ChecklistUI.UpdateChecklist(0, true);
            }

            // Connect to mongodb
            try
            {
                var connectionString = MelonSettings.MongoDbConnectionString;
                var check = CheckMongoDB(connectionString);
                if (!check)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                ChecklistUI.end = true;
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

            // Setup Menu
            DisplayManager.MenuOptions.Add(StringsManager.GetString("FullScanOption"), MelonScanner.Scan);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("ShortScanOption"), MelonScanner.ScanShort);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("DatabaseResetConfirmation"), MelonScanner.ResetDB);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));

            // Plugins
            if (!MelonFlags.DisablePlugins || loadPlugins)
            {
                ChecklistUI.UpdateChecklist(1, true);
                LoadPlugins(mWebApi);
            }

            if (!headless)
            {
                ChecklistUI.UpdateChecklist(2, true);
            }
            Thread.Sleep(200);
            ChecklistUI.end = true;

        }
        public static void LoadSettings()
        {
            string settingstxt = File.ReadAllText($"{melonPath}/Settings.json");
            var set = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(settingstxt);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var services = serviceCollection.BuildServiceProvider();

            var instance = ActivatorUtilities.CreateInstance<Security>(services);
            set.JWTKey = instance._protector.Unprotect(set.JWTKey);

            MelonSettings = set;
        }
        public static void SaveSettings()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDataProtection();
            var services = serviceCollection.BuildServiceProvider();

            var instance = ActivatorUtilities.CreateInstance<Security>(services);
            var set = MelonSettings;
            var key = MelonSettings.JWTKey;
            set.JWTKey = instance._protector.Protect(key);

            string settingstxt = Newtonsoft.Json.JsonConvert.SerializeObject(set, Formatting.Indented);
            File.WriteAllText($"{melonPath}/Settings.json", settingstxt);
        }
        private static void LoadFlags()
        {
            string flagstxt = File.ReadAllText($"{melonPath}/Flags.json");
            MelonFlags = Newtonsoft.Json.JsonConvert.DeserializeObject<Flags>(flagstxt);
        }
        private static void SaveFlags()
        {
            string flagtxt = Newtonsoft.Json.JsonConvert.SerializeObject(MelonFlags, Formatting.Indented);
            File.WriteAllText($"{melonPath}/Flags.json", flagtxt);
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
        private static void LoadPlugins(IWebApi mWebApi)
        {
            if (!Directory.Exists($"{melonPath}/Plugins"))
            {
                Directory.CreateDirectory($"{melonPath}/Plugins");
            }

            var files = Directory.GetFiles($"{melonPath}/Plugins");
            Plugins = files.SelectMany(pluginPath =>
            {
                Assembly pluginAssembly = LoadPlugin(pluginPath);
                return CreatePlugins(pluginAssembly);
            }).ToList();
            var host = new MelonHost() { WebApi = mWebApi };
            foreach (var plugin in Plugins)
            {
                if (plugin == null)
                {
                    continue;
                }

                plugin.LoadMelonCommands(host);
                var check = plugin.Execute();
                if(check != 0)
                {
                    Serilog.Log.Error($"Plugin Execute failed: {plugin.Name}");
                }
            }
        }
        private static Assembly LoadPlugin(string relativePath)
        {
            // Navigate up to the solution root
            string root = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            Path.GetDirectoryName(
                                Path.GetDirectoryName(typeof(Program).Assembly.Location)))))));

            string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
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
