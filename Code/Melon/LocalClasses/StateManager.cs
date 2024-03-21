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
using Amazon.Runtime.Internal.Transform;
using Serilog;
using Microsoft.Owin.Hosting;
using MongoDB.Bson;

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
        public static Dictionary<string, string> LaunchArgs { get; set; }
        public static MelonHost Host { get; set; }
        public static IWebApi WebApi { get; set; }
        public static string Version { get; set; }
        public static string Language { get; set; }
        public static bool RestartServer { get; set; }
        public static bool ServerIsAlive;
        public static bool ConsoleIsAlive;
        public static void Init(IWebApi mWebApi, bool headless, bool conUI)
        {
            Log.Logger = new LoggerConfiguration()
                            .WriteTo.File($"{StateManager.melonPath}/MelonLogs.txt")
                            .CreateLogger();
            // Title
            if (!headless)
            {
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Init" });
                // Setup checklist UI
                ChecklistUI.SetChecklistItems(new[]
                {
                    "Load settings",
                    "Connect to MongoDB",
                    "Load Plugins"
                });
                ChecklistUI.ChecklistDislayToggle();
            }

            Host = new MelonHost();
            WebApi = mWebApi;
            CreateDirectories();
            LoadSettings();
            SetLanguage(LaunchArgs.ContainsKey("lang") ? LaunchArgs["lang"] : "");

            // Reload UI in set language
            if (!headless)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
                // Setup checklist UI
                ChecklistUI.SetChecklistItems(new[]
                {
                    StringsManager.GetString("SettingsLoadStatus"),
                    StringsManager.GetString("MongoDBConnectStatus"),
                    StringsManager.GetString("LoadPluginsStatus")
                });
            }

            LoadFlags();

            if (StateManager.LaunchArgs.ContainsKey("version"))
            {
                if (!headless)
                {
                    ChecklistUI.ChecklistDislayToggle();
                }
                DisplayManager.DisplayVersionInfo();
                Environment.Exit(0);
            }

            DisplayManager.SetHelpOptions();

            // Plugin's help options
            if (!MelonFlags.DisablePlugins && !LaunchArgs.ContainsKey("disablePlugins"))
            {
                if (File.Exists($"{melonPath}/Configs/DisabledPlugins.json"))
                {
                    DisabledPlugins = Storage.LoadConfigFile<List<string>>("DisabledPlugins", null, out _);
                }
                else
                {
                    DisabledPlugins = new List<string>();
                    Storage.SaveConfigFile("DisabledPlugins", DisabledPlugins, null);
                }
                PluginsContexts = new List<PluginLoadContext>();

                PluginsManager.LoadPlugins();
            }

            // Show help menu
            if (LaunchArgs.ContainsKey("help"))
            {
                if (!headless)
                {
                    ChecklistUI.ChecklistDislayToggle();
                }
                DisplayManager.DisplayHelp();
                Environment.Exit(0);
            }

            

            // Load SSLConfig if exists
            var config = Storage.LoadConfigFile<SSLConfig>("SSLConfig", new[] { "Password" }, out _);
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
            DisplayManager.MenuOptions = new System.Collections.Specialized.OrderedDictionary();
            if (!check)
            {
                if (!headless)
                {
                    ChecklistUI.ChecklistDislayToggle();
                    Thread.Sleep(200);
                    MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
                }

                Console.WriteLine(StringsManager.GetString("MongoDBConnectionError").Pastel(MelonColor.Error));

                if (!headless && conUI)
                {
                    Console.WriteLine(StringsManager.GetString("ReturnPrompt").Pastel(MelonColor.BackgroundText));
                    Console.ReadKey(intercept: true);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("MongoDBConnectionEditOption"), SettingsUI.MongoDBSettings);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));
                    return;
                }
                else
                {
                    //Environment.Exit(1);
                }
            }

            // Check database version compatibility
            var compatible = DbVersionManager.CheckVersionCompatibility();
            if (compatible != "" && !LaunchArgs.ContainsKey("allowConversion"))
            {
                if (!headless)
                {
                    ChecklistUI.ChecklistDislayToggle();
                    Thread.Sleep(200);
                    MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
                }

                Console.WriteLine(StringsManager.GetString("IncompatibleCollections").Pastel(MelonColor.Text));
                Console.WriteLine($"[{compatible}]".Pastel(MelonColor.Highlight));

                if (!headless && conUI)
                {
                    Console.WriteLine(StringsManager.GetString("ConvertCollectionsPrompt").Pastel(MelonColor.Text));
                    Console.WriteLine($"({StringsManager.GetString("ConversionWarning")})".Pastel(MelonColor.Text));
                    var choice = MelonUI.OptionPicker(new List<string>() { StringsManager.GetString("PositiveConfirmation"), StringsManager.GetString("NegativeConfirmation") });
                    if (choice == StringsManager.GetString("PositiveConfirmation"))
                    {
                        DbVersionManager.ConvertCollectionsUI(compatible.Split(","));
                        ChecklistUI.SetChecklistItems(new[]
                        {
                            StringsManager.GetString("SettingsLoadStatus"),
                            StringsManager.GetString("MongoDBConnectStatus"),
                            StringsManager.GetString("LoadPluginsStatus")
                        });
                        MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
                        ChecklistUI.ChecklistDislayToggle();
                        ChecklistUI.UpdateChecklist(0, true);
                    }
                    else
                    {
                        Environment.Exit(3);
                    }
                }
                else
                {
                    Console.WriteLine($"({StringsManager.GetString("ConversionWarning")})");
                }
            }
            else if (compatible != "" && LaunchArgs.ContainsKey("allowConversion"))
            {
                if (!headless)
                {
                    ChecklistUI.ChecklistDislayToggle();
                    Thread.Sleep(200);
                    MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
                }
                if (!headless)
                {
                    DbVersionManager.ConvertCollectionsUI(compatible.Split(","));
                    ChecklistUI.SetChecklistItems(new[]
                    {
                        StringsManager.GetString("SettingsLoadStatus"),
                        StringsManager.GetString("MongoDBConnectStatus"),
                        StringsManager.GetString("LoadPluginsStatus")
                    });
                    MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("InitializationStatus") });
                    ChecklistUI.ChecklistDislayToggle();
                    ChecklistUI.UpdateChecklist(0, true);
                }
                else
                {
                    DbVersionManager.ConvertCollections(compatible.Split(","));
                }
            }

            // Check for users
            if (DbClient.GetDatabase("Melon").GetCollection<BsonDocument>("Users").CountDocuments(new BsonDocument()) == 0)
            {
                DisplayManager.UIExtensions.Add("SetupUI", SetupUI.Display);
            }

            // Start Queues Cleaner
            QueuesCleaner.StartCleaner();

            // Setup Display Options
            DisplayManager.MenuOptions.Add(StringsManager.GetString("FullScanOption"), MelonScanner.MemoryScan);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("ShortScanOption"), MelonScanner.MemoryScanShort);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
            DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => 
            {
                Environment.Exit(0);
            });

            if (!headless)
            {
                ChecklistUI.UpdateChecklist(1, true);
            }

            // Plugins
            if (!MelonFlags.DisablePlugins && !LaunchArgs.ContainsKey("disablePlugins"))
            {
                if (WebApi == null)
                {
                    PluginsManager.LoadPluginUIs();
                }
                else
                {
                    PluginsManager.ExecutePlugins();
                }
            }

            if (!headless)
            {
                ChecklistUI.UpdateChecklist(2, true);
                ChecklistUI.ChecklistDislayToggle();
                Thread.Sleep(200);
            }

        }
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
                    QueueCleanupWaitInHours = 48
                };
                MelonColor.SetDefaults(); 
                Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
            }
            else
            {
                // Load settings
                MelonSettings = Storage.LoadConfigFile<Settings>("MelonSettings", new[] { "JWTKey" }, out _);


                if (MelonSettings == null)
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
                        QueueCleanupWaitInHours = 48
                    };
                }

                if (MelonSettings.QueueCleanupWaitInHours == 0)
                {
                    MelonSettings.QueueCleanupWaitInHours = 48;
                }

                if (MelonSettings.DefaultLanguage.IsNullOrEmpty())
                {
                    MelonSettings.DefaultLanguage = "EN";
                }

                if (MelonSettings.JWTKey.IsNullOrEmpty())
                {
                    MelonSettings.JWTKey = Security.GenerateSecretKey();
                }

                if (MelonSettings.ListeningURL.IsNullOrEmpty())
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

                Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
            }
        }
        private static void SetLanguage(string language)
        {
            if (language == "")
            {
                language = MelonSettings.DefaultLanguage;
            }

            var resources = typeof(StateManager).Assembly.GetManifestResourceNames();
            if (resources.Contains($"Melon.Strings.UIStrings{language.ToUpper()}.resources"))
            {
                StringsManager = new ResourceManager($"Melon.Strings.UIStrings{language.ToUpper()}", typeof(StateManager).Assembly);
                Language = language.ToUpper();
            }
            else
            {
                StringsManager = new ResourceManager($"Melon.Strings.UIStringsEN", typeof(StateManager).Assembly);
                Language = "EN";
            }
        }
        private static void CreateDirectories()
        {
            Directory.CreateDirectory($"{melonPath}/Configs");
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
                MelonFlags = Storage.LoadConfigFile<Flags>("MelonFlags", null, out _);
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

            var stream = typeof(StateManager).Assembly.GetManifestResourceStream("Melon.Assets.defaultArtwork.png");
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        public static void ParseArgs(string[] args)
        {
            LaunchArgs = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Replace("-", "");
                if (!args[i].StartsWith("-"))
                {
                    LaunchArgs[LaunchArgs.Last().Key] += $" {arg}";
                    break;
                }

                if (i + 1 >= args.Length)
                {
                    LaunchArgs.Add(arg, "");
                    break;
                }

                if (args[i + 1].StartsWith("-"))
                {
                    LaunchArgs.Add(arg, "");
                }
                else
                {
                    string parameter = args[i + 1];
                    LaunchArgs.Add(arg, parameter);
                    i++;
                }
            }

            // Combine Known Args
            if (LaunchArgs.ContainsKey("h"))
            {
                LaunchArgs.Remove("h");
                LaunchArgs.TryAdd("headless", "");
            }

            if (LaunchArgs.ContainsKey("s"))
            {
                LaunchArgs.Remove("s");
                LaunchArgs.TryAdd("setup", "");
            }

            if (LaunchArgs.ContainsKey("d"))
            {
                LaunchArgs.Remove("d");
                LaunchArgs.TryAdd("disablePlugins", "");
            }

            if (LaunchArgs.ContainsKey("v"))
            {
                LaunchArgs.Remove("v");
                LaunchArgs.TryAdd("version", "");
            }

            if (LaunchArgs.ContainsKey("l"))
            {
                LaunchArgs.TryAdd("lang", LaunchArgs["l"]);
                LaunchArgs.Remove("l");
            }
            if (LaunchArgs.ContainsKey("c"))
            {
                LaunchArgs.Remove("c");
                LaunchArgs.TryAdd("allowConversion", "");
            }

            if (LaunchArgs.ContainsKey("version") || LaunchArgs.ContainsKey("help"))
            {
                LaunchArgs.TryAdd("headless", "");
            }
        }
    }
}
