using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson.IO;
using System.Text.Json;
using System.Drawing;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Resources;
using Microsoft.IdentityModel.Tokens;
using Melon.Models;
using System.Reflection;
using System.Windows.Input;
using Melon.Interface;
using Melon.PluginModels;
using Serilog;
using MongoDB.Bson;
using Microsoft.Extensions.FileSystemGlobbing;

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
        public static string Branch { get; set; }
        public static string Language { get; set; }
        public static byte[] JWTKey { get; set; }
        public static void Initialize(IWebApi webApi)
        {
            Host = new MelonHost();
            WebApi = webApi;
            CreateDirectories();
            JWTKey = AuthSessions.LoadOrCreateKey(); // persisted, so restarts no longer sign everyone out
            LoadSettings();
            SetLanguage(LaunchArgs.GetValueOrDefault("lang", ""));
            LoadFlags();
            Security.SetSSLConfig(Storage.LoadConfigFile<SSLConfig>("SSLConfig", new[] { "Password" }, out _) ?? new SSLConfig());
            DisabledPlugins = Storage.LoadConfigFile<List<string>>("DisabledPlugins", null, out _) ?? [];
            ArtworkService.Instance.LoadConfig();
            VisualsService.Instance.LoadConfig();
            Plugins = [];
            PluginsContexts = [];

        }

        internal static MongoClient CreateDatabaseClient(string connectionString)
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        }

        private static void LoadSettings()
        {
            var defaults = new Settings
            {
                MongoDbConnectionString = "mongodb://localhost:27017",
                LibraryPaths = [], ArtistSplitIndicators = [",", ";", "/", "feat.", "ft.", "&"],
                GenreSplitIndicators = [",", ";", "/", @"\\", "//"],
                Text = Color.FromArgb(204,204,204), ShadedText = Color.FromArgb(100,100,100),
                BackgroundText = Color.FromArgb(66,66,66), Highlight = Color.FromArgb(97,214,214),
                Melon = Color.FromArgb(26,225,19), Error = Color.Red,
                ListeningURL = "http://localhost:14524", DefaultLanguage = "EN", JWTExpireInMinutes = 60,
                UseMenuColor = true, QueueCleanupWaitInHours = 48, NetworkMode = "Automatic"
            };
            var loaded = Storage.LoadConfigFile<Settings>("MelonSettings", null, out _);
            MelonSettings = loaded ?? defaults;
            if (MelonSettings.MongoDbConnectionString == ManagedMongo.Alias)
            {
                try { MelonSettings.MongoDbConnectionString = ManagedMongo.SavedConnection(); }
                catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException)
                {
                    // Keep the alias so local administration can replace the unavailable connection.
                    Serilog.Log.Warning("Managed MongoDB credentials are unavailable.");
                }
            }
            MelonSettings.LibraryPaths ??= [];
            MelonSettings.ArtistSplitIndicators ??= defaults.ArtistSplitIndicators;
            MelonSettings.GenreSplitIndicators ??= defaults.GenreSplitIndicators;
            if (string.IsNullOrEmpty(MelonSettings.DefaultLanguage)) MelonSettings.DefaultLanguage = defaults.DefaultLanguage;
            if (string.IsNullOrEmpty(MelonSettings.ListeningURL)) MelonSettings.ListeningURL = defaults.ListeningURL;
            // Settings saved before automatic networking keep working exactly as they were set up.
            if (MelonSettings.NetworkMode is not ("Automatic" or "Custom")) MelonSettings.NetworkMode = "Custom";
            MelonSettings.RemoteAccess ??= new();
            MelonSettings.RemoteAccess.PkarrRelays ??= [];
            MelonSettings.BlockedAddresses ??= [];
            MelonSettings.PublicAddressHistory ??= [];
            MelonSettings.BroadcastName ??= "";
            if (MelonSettings.HttpPort is < 1 or > 65535) MelonSettings.HttpPort = 14524;
            if (MelonSettings.HttpsPort is < 1 or > 65535) MelonSettings.HttpsPort = 14525;
            if (loaded == null)
            {
                File.WriteAllText(Path.Combine(melonPath, "Configs", "setup.pending"), "");
                Storage.SaveConfigFile("MelonSettings", MelonSettings, null);
            }
        }

        internal static void SetLanguage(string language)
        {
            // The wire language stays stable. Controllers translate it for their own UI.
            Language = language.Length == 0 ? MelonSettings.DefaultLanguage : language;
            StringsManager = new ResourceManager("Melon.Strings.UIStringsEN", typeof(StateManager).Assembly);

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
