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

namespace Melon.LocalClasses
{
    /// <summary>
    /// Handles melon's state, such as settings.
    /// </summary>
    public static class StateManager
    {
        public static string melonPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Melon";
        public static MongoClient DbClient;
        public static Settings MelonSettings { get; set; }
        public static Flags MelonFlags { get; set; }
        private static Process serverProcess;
        public static void Init()
        {
            // Title
            MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Init" });

            // Setup checklist UI
            ChecklistUI.CreateChecklist(new List<string>()
            {
                "Load settings",
                "Connect to mongodb"
            });
            ChecklistUI.ShowChecklist();

            // Load Settings
            if (!Directory.Exists(melonPath))
            {
                Directory.CreateDirectory(melonPath);
            }

            if (!Directory.Exists($"{StateManager.melonPath}/AlbumArts"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/AlbumArts");
            }

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
            if (MelonFlags.ForceOOBE)
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
                    UseMenuColor = true,
                };
                SaveSettings();
                MelonColor.SetDefaults();
                DisplayManager.UIExtensions.Add(SetupUI.Display);
            }
            else
            {
                try
                {
                    LoadSettings();

                    MelonColor.Text = MelonSettings.Text;
                    MelonColor.ShadedText = MelonSettings.ShadedText;
                    MelonColor.BackgroundText = MelonSettings.BackgroundText;
                    MelonColor.Highlight = MelonSettings.Highlight;
                    MelonColor.Melon = MelonSettings.Melon;
                    MelonColor.Error = MelonSettings.Error;

                    if(MelonSettings.JWTKey == null || MelonSettings.JWTKey == "")
                    {
                        MelonSettings.JWTKey = Security.GenerateSecretKey();
                        DisplayManager.UIExtensions.Add(SetupUI.Display);
                    }
                    SaveSettings();
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
                        UseMenuColor = true,
                    };
                    SaveSettings();
                    MelonColor.SetDefaults();
                    DisplayManager.UIExtensions.Add(SetupUI.Display);
                }

            }
            ChecklistUI.UpdateChecklist(0, true);

            // Connect to mongodb
            try
            {
                var connectionString = MelonSettings.MongoDbConnectionString;
                DbClient = new MongoClient(connectionString);
                var MelonDB = DbClient.GetDatabase("Melon");
            }
            catch (Exception)
            {
                ChecklistUI.end = true;
                Thread.Sleep(200);
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Init" });
                Console.WriteLine("Error: Couldn't Connect to MongoDB, is the connection string correct?".Pastel(MelonColor.Error));
                Console.WriteLine("Press any key to return to the menu".Pastel(MelonColor.BackgroundText));
                Console.ReadKey(intercept: true);
                DisplayManager.MenuOptions.Add("Settings", SettingsUI.Settings);
                DisplayManager.MenuOptions.Add("Exit", () => Environment.Exit(0));
                return;
            }

            // Setup Menu
            DisplayManager.MenuOptions.Add("Full Scan", MelonScanner.Scan);
            DisplayManager.MenuOptions.Add("Short Scan", MelonScanner.ScanShort);
            DisplayManager.MenuOptions.Add("Reset DB", MelonScanner.ResetDB);
            //DisplayManager.MenuOptions.Add("Library View", () =>  { LibraryUI.LibrarySearch(); });
            DisplayManager.MenuOptions.Add("Settings", SettingsUI.Settings);
            DisplayManager.MenuOptions.Add("Exit", () => Environment.Exit(0));
            ChecklistUI.UpdateChecklist(1, true);
            ChecklistUI.end = true;
            Thread.Sleep(200);

        }
        public static void LoadSettings()
        {
            string settingstxt = File.ReadAllText($"{melonPath}/Settings.json");
            MelonSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(settingstxt);
        }
        public static void SaveSettings()
        {
            string settingstxt = Newtonsoft.Json.JsonConvert.SerializeObject(MelonSettings);
            File.WriteAllText($"{melonPath}/Settings.json", settingstxt);
        }
        public static void LoadFlags()
        {
            string flagstxt = File.ReadAllText($"{melonPath}/Flags.json");
            MelonFlags = Newtonsoft.Json.JsonConvert.DeserializeObject<Flags>(flagstxt);
        }
        public static void SaveFlags()
        {
            string flagtxt = Newtonsoft.Json.JsonConvert.SerializeObject(MelonFlags);
            File.WriteAllText($"{melonPath}/Flags.json", flagtxt);
        }
        public static bool CheckMongoDB(string connectionString)
        {
            try
            {
                DbClient = new MongoClient(connectionString);
                if(DbClient.Cluster.Description.State == MongoDB.Driver.Core.Clusters.ClusterState.Connected)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static void StartServer()
        {
            serverProcess = new Process();
            serverProcess.StartInfo.FileName = $"";

            serverProcess.Start();

            // Wait for the server to start
            Thread.Sleep(2000);
        }

        public static void StopServer()
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.CloseMainWindow();
                serverProcess.WaitForExit();
                serverProcess.Dispose();
            }
        }
    }
}
