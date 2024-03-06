using Amazon.Runtime.Internal.Transform;
using Melon.Classes;
using Melon.Interface;
using Melon.LocalClasses;
using Melon.Models;
using Melon.PluginModels;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Melon.LocalClasses.StateManager;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// Contains all the UI for adjusting settings.
    /// </summary>
    public static class SettingsUI
    {
        public static OrderedDictionary MenuOptions = new OrderedDictionary();

        // Settings Main
        public static void Settings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var settings = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("ScannerSettingsOption"), ScannerSettings },
                    { StringsManager.GetString("NetworkSettingsOption"), Networking },
                    { StringsManager.GetString("MenuCustomizationOption"), MenuCustomization },
                    { StringsManager.GetString("PluginsOption"), PluginsMenu },
                    { StringsManager.GetString("DatabaseMenu"), DatabaseSettings },
                    { StringsManager.GetString("OpenMelonFolderOption") , OpenMelonFolder }
                };

            while (LockUI && !StateManager.RestartServer)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in settings.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)settings[choice])();
            }
        }

        // Database Settings
        private static void DatabaseSettings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var options = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("DbBackupOption"), Transfer.ExportDb },
                    { StringsManager.GetString("DbLoadBackupOption"), Transfer.ImportDbUI },
                    { StringsManager.GetString("DatabaseResetConfirmation"), MelonScanner.ResetDBUI},
                    { StringsManager.GetString("PlaylistExportOption"), Transfer.ExportPlaylistUI },
                    { StringsManager.GetString("PlaylistImportOption"), Transfer.ImportPlaylistUI },
                };

            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("DatabaseMenu") });

                // Input
                var commands = new List<string>();
                foreach (var key in options.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }
                else if (choice == StringsManager.GetString("DbBackupOption"))
                {
                    ((Func<bool>)options[choice])();
                }
                else
                {
                    ((Action)options[choice])();
                }
            }
        }

        // Scanner Settings
        private static void ScannerSettings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var settings = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("LibraryPathEditOption") , LibraryPathSettings }
                };
            while (LockUI && !StateManager.RestartServer)
            {
                if (((Action)DisplayManager.MenuOptions[StringsManager.GetString("FullScanOption")]).Method.Name != "Scan")
                {
                    settings.Add(StringsManager.GetString("ExperimentalScannerDisable"), () =>
                    {
                        DisplayManager.MenuOptions[StringsManager.GetString("FullScanOption")] = MelonScanner.Scan;
                        DisplayManager.MenuOptions[StringsManager.GetString("ShortScanOption")] = MelonScanner.ScanShort;
                        settings.Remove(StringsManager.GetString("ExperimentalScannerDisable"));
                    });
                }
                else
                {
                    settings.Add(StringsManager.GetString("ExperimentalScannerEnable"), () =>
                    {
                        DisplayManager.MenuOptions[StringsManager.GetString("FullScanOption")] = MelonMemoryScanner.MemoryScan;
                        DisplayManager.MenuOptions[StringsManager.GetString("ShortScanOption")] = MelonMemoryScanner.MemoryScanShort;
                        settings.Remove(StringsManager.GetString("ExperimentalScannerEnable"));
                    });
                }

                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("ScannerSettingsOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in settings.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)settings[choice])();
            }
        }
        private static void LibraryPathSettings()
        {
            while (true)
            {
                // title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("ScannerSettingsOption"), StringsManager.GetString("LibraryOption") });
                Console.WriteLine($"({StringsManager.GetString("PathDeletionSelection")})".Pastel(MelonColor.Text));

                // Get paths
                List<string> NewPaths = new List<string>();
                NewPaths.Add(StringsManager.GetString("BackNavigation"));
                NewPaths.Add(StringsManager.GetString("PathAddition"));
                NewPaths.AddRange(StateManager.MelonSettings.LibraryPaths);

                // Add Options

                // Get Selection
                string input = MelonUI.OptionPicker(NewPaths);
                if (input == StringsManager.GetString("PathAddition"))
                {
                    // For showing error color when directory doesn't exist
                    bool showPathError = false;
                    while (true)
                    {
                        // Title
                        MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("ScannerSettingsOption"), StringsManager.GetString("LibraryOption"), StringsManager.GetString("LibraryAddMenu") });

                        // Description and Input UI
                        if (showPathError)
                        {
                            Console.WriteLine(StringsManager.GetString("PathValidationError").Pastel(MelonColor.Error));
                            showPathError = false;
                        }
                        Console.WriteLine(StringsManager.GetString("LibraryPathEntry").Pastel(MelonColor.Text));
                        Console.Write($"> ".Pastel(MelonColor.Text));

                        // Get Path
                        string path = Console.ReadLine();
                        if (path == "")
                        {
                            break;
                        }

                        // If path is valid, add it to library paths and save
                        if (!Directory.Exists(path))
                        {
                            showPathError = true;
                        }
                        else
                        {
                            StateManager.MelonSettings.LibraryPaths.Add(path);
                            Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
                            break;
                        }
                    }
                }
                else if (input == StringsManager.GetString("BackNavigation"))
                {
                    // Leave
                    return;
                }
                else
                {
                    // Remove selected library path
                    StateManager.MelonSettings.LibraryPaths.Remove(input);
                    Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
                }
            }
        }

        // Settings Networking
        private static void Networking()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var settings = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("MongoDBConnectionEditOption"), MongoDBSettings },
                    { StringsManager.GetString("ListeningURLEditOption"), ChangeListeningURL },
                    { StringsManager.GetString("HTTPSConfigOption"), HTTPSSetup },
                };

            while (LockUI && !StateManager.RestartServer)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("NetworkSettingsOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in settings.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)settings[choice])();
            }
        }
        public static void MongoDBSettings()
        {
            bool check = true;
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("NetworkSettingsOption"), StringsManager.GetString("MongoDBOption") });

                // Description
                Console.WriteLine($"{StringsManager.GetString("MongoDBConnectionString")}: {MelonSettings.MongoDbConnectionString.Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
                Console.WriteLine($"({StringsManager.GetString("StringEntryPrompt")})".Pastel(MelonColor.Text));
                if (!check)
                {
                    Console.WriteLine($"[{StringsManager.GetString("MongoDBConnectionError")}]".Pastel(MelonColor.Error));
                }
                check = false;

                // Get New MongoDb Connection String
                Console.Write("> ".Pastel(MelonColor.Text));
                string input = Console.ReadLine();
                if (input == "")
                {
                    return;
                }

                check = CheckMongoDB(input);
                if (check)
                {
                    // Set and Save new conn string
                    MelonSettings.MongoDbConnectionString = input;
                    Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
                    if (!DisplayManager.MenuOptions.Contains(StringsManager.GetString("FullScanOption")))
                    {
                        DisplayManager.MenuOptions.Clear();
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("FullScanOption"), MelonMemoryScanner.MemoryScan);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("ShortScanOption"), MelonMemoryScanner.MemoryScanShort);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));
                    }
                    break;
                }

            }

        }
        private static void ChangeListeningURL()
        {
            bool result = true;
            while (!StateManager.RestartServer)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("NetworkSettingsOption"), StringsManager.GetString("ListeningURLDisplay") });

                // Description
                Console.WriteLine(StringsManager.GetString("ServerRestartWarning").Pastel(MelonColor.Highlight));
                Console.WriteLine($"{StringsManager.GetString("URLDisplay")}: {StateManager.MelonSettings.ListeningURL.Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
                Console.WriteLine($"({StringsManager.GetString("URLListEntry")})".Pastel(MelonColor.Text));
                if (!result)
                {
                    Console.WriteLine($"[{StringsManager.GetString("URLValidationError")}]".Pastel(MelonColor.Error));
                }
                result = false;

                // Get New URL
                Console.Write("> ".Pastel(MelonColor.Text));
                string input = Console.ReadLine();
                if (input == "")
                {
                    return;
                }

                foreach (var url in input.Split(";"))
                {
                    Regex UrlWithWildcardRegex = new Regex(@"^(https?:\/\/)([\w*]+\.)*[\w*]+(:\d+)?(\/[\w\/]*)*(\?.*)?(#.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    result = UrlWithWildcardRegex.IsMatch(url);

                    if (result == false)
                    {
                        break;
                    }
                }

                if (result)
                {
                    // Set and Save new conn string
                    StateManager.MelonSettings.ListeningURL = input;
                    Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
                    StateManager.RestartServer = true;
                    foreach (var plugin in Plugins)
                    {
                        plugin.Unload();
                    }
                    break;
                }

            }

        }
        private static void HTTPSSetup()
        {
            // Check if ssl is setup already
            var config = Security.GetSSLConfig();
            if (config.PathToCert != "")
            {
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("NetworkSettingsOption"), StringsManager.GetString("HTTPSConfigOption") });
                Console.WriteLine(StringsManager.GetString("ServerRestartWarning").Pastel(MelonColor.Highlight));
                Console.WriteLine(StringsManager.GetString("SSLConfigStatus").Pastel(MelonColor.Text));
                var opt = MelonUI.OptionPicker(new List<string>() { StringsManager.GetString("BackNavigation"), StringsManager.GetString("SSLDisableOption"), StringsManager.GetString("SSLConfigEditOption") });
                if (opt == StringsManager.GetString("BackNavigation"))
                {
                    return;
                }
                else if (opt == StringsManager.GetString("SSLDisableOption"))
                {
                    Security.SetSSLConfig("", "");
                    Storage.SaveConfigFile("SSLConfig", Security.GetSSLConfig(), new[] { "Password" });
                    StateManager.RestartServer = true;
                    foreach (var plugin in Plugins)
                    {
                        plugin.Unload();
                    }
                }

            }

            bool result = true;
            while (!StateManager.RestartServer)
            {
                // Get the Path to the pfx
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("NetworkSettingsOption"), StringsManager.GetString("HTTPSConfigOption") });
                Console.WriteLine(StringsManager.GetString("ServerRestartWarning").Pastel(MelonColor.Highlight));
                Console.WriteLine(StringsManager.GetString("HTTPSRequirementNote").Pastel(MelonColor.Text));
                Console.WriteLine($"{StringsManager.GetString("SSLCertificatePathEntryFirst")} {".pfx".Pastel(MelonColor.Highlight)} {StringsManager.GetString("SSLCertificatePathEntrySecond")}:".Pastel(MelonColor.Text));
                if (!result)
                {
                    Console.WriteLine($"[{StringsManager.GetString("CertificatePasswordError")}]".Pastel(MelonColor.Error));
                }
                result = false;

                Console.Write("> ".Pastel(MelonColor.Text));
                string pathToCert = Console.ReadLine();
                if (pathToCert == "")
                {
                    return;
                }

                // Get the password to the cert
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("NetworkSettingsOption"), StringsManager.GetString("HTTPSConfigOption") });
                Console.WriteLine(StringsManager.GetString("SSLPasswordEntry").Pastel(MelonColor.Text));

                Console.Write("> ".Pastel(MelonColor.Text));
                string password = MelonUI.HiddenInput();
                if (password == "")
                {
                    return;
                }

                // Check if cert and password are valid
                try
                {
                    var certificate = new X509Certificate2(pathToCert, password);
                    result = true;
                }
                catch (Exception)
                {
                    result = false;
                }


                if (result)
                {
                    // Set and Save new conn string
                    Security.SetSSLConfig(pathToCert, password);
                    Storage.SaveConfigFile("SSLConfig", Security.GetSSLConfig(), new[] { "Password" });
                    StateManager.RestartServer = true;
                    foreach (var plugin in Plugins)
                    {
                        plugin.Unload();
                    }
                    break;
                }

            }

        }

        // Settings Customization
        private static void MenuCustomization()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var settings = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("DefaultLanguageOption"), ChangeDeafultLanguage },
                    { StringsManager.GetString("ColorEditOption") , ChangeMelonColors }
                };

            while (LockUI && !StateManager.RestartServer)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("MenuCustomizationOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in settings.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)settings[choice])();
            }
        }
        private static void ChangeDeafultLanguage()
        {
            bool check = true;
            string input = "";
            while (!StateManager.RestartServer)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("MenuCustomizationOption"), StringsManager.GetString("DefaultLanguageOption") });

                // Description
                Console.WriteLine(StringsManager.GetString("ServerRestartWarning").Pastel(MelonColor.Highlight));
                Console.WriteLine($"{StringsManager.GetString("CurrentLanguageDefault")}: {MelonSettings.DefaultLanguage.Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
                Console.WriteLine($"({StringsManager.GetString("LanguageInstructions")})".Pastel(MelonColor.Text));
                if (!check)
                {
                    Console.WriteLine($"[{StringsManager.GetString("LanguageError").Replace("{}", input)}]".Pastel(MelonColor.Error));
                }
                check = false;

                // Get New MongoDb Connection String
                Console.Write("> ".Pastel(MelonColor.Text));
                input = Console.ReadLine();
                if (input == "")
                {
                    return;
                }

                var resources = typeof(SettingsUI).Assembly.GetManifestResourceNames();
                if (resources.Contains($"Melon.Strings.UIStrings{input.ToUpper()}.resources"))
                {
                    MelonSettings.DefaultLanguage = input;
                    StringsManager = StringsManager = new ResourceManager($"Melon.Strings.UIStrings{input.ToUpper()}", typeof(SettingsUI).Assembly);
                    Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
                    StateManager.RestartServer = true;
                    foreach (var plugin in Plugins)
                    {
                        plugin.Unload();
                    }
                    break;
                }
            }

        }
        public static void ChangeMelonColors()
        {
            while (true)
            {
                Dictionary<string, int> ColorMenuOptions = new Dictionary<string, int>()
                {
                    { $"{StringsManager.GetString("BackNavigation")}" , 7 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("NormalTextColorSetting").Pastel(MelonColor.Text)}", 0 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("ShadedTextColorSetting").Pastel(MelonColor.ShadedText)}", 1 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("BackgroundTextColorSetting").Pastel(MelonColor.BackgroundText)}", 2 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("MelonTitleColorSetting").Pastel(MelonColor.Melon)}", 3 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("HighlightColorSetting").Pastel(MelonColor.Highlight)}", 4 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("ErrorColorSetting").Pastel(MelonColor.Error)}", 5 },
                    { StringsManager.GetString("DefaultColorReset"), 6 }
                };
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("MenuCustomizationOption"), StringsManager.GetString("LibraryOption"), StringsManager.GetString("ColorOptions") });
                Console.WriteLine($"{StringsManager.GetString("ColorSelection")}:".Pastel(MelonColor.Text));
                var choice = MelonUI.OptionPicker(ColorMenuOptions.Keys.ToList());
                Thread.Sleep(100);
                Color newClr = new Color();

                Console.SetCursorPosition(0, 1);

                switch (ColorMenuOptions[choice])
                {
                    case 0:
                        newClr = MelonUI.ColorPicker(MelonColor.Text);
                        MelonColor.Text = newClr;
                        StateManager.MelonSettings.Text = newClr;
                        break;
                    case 1:
                        newClr = MelonUI.ColorPicker(MelonColor.ShadedText);
                        MelonColor.ShadedText = newClr;
                        StateManager.MelonSettings.ShadedText = newClr;
                        break;
                    case 2:
                        newClr = MelonUI.ColorPicker(MelonColor.BackgroundText);
                        MelonColor.BackgroundText = newClr;
                        StateManager.MelonSettings.BackgroundText = newClr;
                        break;
                    case 3:
                        newClr = MelonUI.ColorPicker(MelonColor.Melon);
                        MelonColor.Melon = newClr;
                        StateManager.MelonSettings.Melon = newClr;
                        break;
                    case 4:
                        newClr = MelonUI.ColorPicker(MelonColor.Highlight);
                        MelonColor.Highlight = newClr;
                        StateManager.MelonSettings.Highlight = newClr;
                        break;
                    case 5:
                        newClr = MelonUI.ColorPicker(MelonColor.Error);
                        MelonColor.Error = newClr;
                        StateManager.MelonSettings.Error = newClr;
                        break;
                    case 6:
                        MelonColor.SetDefaults();
                        break;
                    case 7:
                        return;
                }
                Storage.SaveConfigFile<Settings>("MelonSettings", MelonSettings, new[] { "JWTKey" });
            }
        }

        // Settings Plugins
        private static void PluginsMenu()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var settings = new OrderedDictionary()
                {
                    { StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { StringsManager.GetString("ViewPluginsOption") , ViewPlugins },
                    { StringsManager.GetString("PluginsSettingsOption") , PluginSettings },
                    { StringsManager.GetString("RescanPluginsFolderOption") , RescanPlugins },
                    { StringsManager.GetString("ReloadAllPluginsOption") , ReloadPlugins }
                };

            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in settings.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)settings[choice])();
            }
        }
        private static void ViewPlugins()
        {
            while (true)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption"), StringsManager.GetString("ViewPluginsOption") });
                List<string> options = new List<string>();
                options.Add(StringsManager.GetString("BackNavigation"));
                foreach (var plugin in Plugins)
                {
                    options.Add(plugin.Name);
                }
                var choice = MelonUI.OptionPicker(options);
                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    return;
                }
                else if (Plugins.Any(x => x.Name == choice))
                {
                    var plugin = Plugins.Where(x => x.Name == choice).FirstOrDefault();
                    ViewPluginInfo(plugin);
                }
            }
        }
        private static void PluginSettings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;

            try
            {
                MenuOptions.Insert(0, StringsManager.GetString("BackNavigation"), () => { LockUI = false; });
            }
            catch (Exception)
            {

            }

            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption"), StringsManager.GetString("PluginsSettingsOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in MenuOptions.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)MenuOptions[choice])();
            }
            MenuOptions.Remove(StringsManager.GetString("BackNavigation"));
        }
        public static void OpenMelonFolder()
        {
            var path = $"{StateManager.melonPath}";
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // For Windows, use explorer.exe
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path.Replace("/", "\\")}\"",
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // For Linux, use xdg-open
                    Process.Start("xdg-open", path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // For macOS, use open
                    Process.Start("open", path);
                }
            }
            catch (Exception)
            {

            }
        }
        private static void ViewPluginInfo(IPlugin plugin)
        {
            while (true)
            {
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption"), plugin.Name });
                Console.WriteLine($"{StringsManager.GetString("NameDisplay")}: {plugin.Name}");
                Console.WriteLine($"{StringsManager.GetString("AuthorsDisplay")}: {plugin.Authors}");
                Console.WriteLine($"{StringsManager.GetString("VersionDisplay")}: {plugin.Version}");
                Console.WriteLine($"{StringsManager.GetString("DescriptionDisplay")}: {plugin.Description}");
                List<string> options = new List<string>();
                options.Add(StringsManager.GetString("BackNavigation"));
                if (DisabledPlugins.Contains($"{plugin.Name}:{plugin.Authors}"))
                {
                    options.Add(StringsManager.GetString("EnablePluginOption"));
                }
                else
                {
                    options.Add(StringsManager.GetString("DisablePluginOption"));
                }

                var choice = MelonUI.OptionPicker(options);

                if (choice == StringsManager.GetString("BackNavigation"))
                {
                    return;
                }
                else if (choice == StringsManager.GetString("DisablePluginOption"))
                {
                    plugin.Unload();
                    DisabledPlugins.Add($"{plugin.Name}:{plugin.Authors}");
                    Storage.SaveConfigFile("DisabledPlugins.json", DisabledPlugins, null);
                }
                else if (choice == StringsManager.GetString("EnablePluginOption"))
                {
                    plugin.Load();
                    DisabledPlugins.Remove($"{plugin.Name}:{plugin.Authors}");
                    Storage.SaveConfigFile("DisabledPlugins.json", DisabledPlugins, null);
                }
            }
        }
        private static void RescanPlugins()
        {
            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption"), StringsManager.GetString("RescanPluginsFolderOption") });
            MelonUI.ShowIndeterminateProgress();
            foreach (var plugin in Plugins)
            {
                plugin.Unload();
            }
            PluginsManager.LoadPlugins();
            MelonUI.HideIndeterminateProgress();
        }
        private static void ReloadPlugins()
        {
            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption"), StringsManager.GetString("ReloadAllPluginsOption") });
            MelonUI.ShowIndeterminateProgress();
            foreach (var plugin in Plugins)
            {
                plugin.Unload();
            }
            foreach (var plugin in Plugins)
            {
                plugin.Load();
            }
            MelonUI.HideIndeterminateProgress();
        }

    }
}
