using Amazon.Runtime.Internal.Transform;
using Melon.Classes;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Resources;
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
        public static OrderedDictionary MenuOptions = new OrderedDictionary()
                {
                    { StringsManager.GetString("MongoDBConnectionEditOption"), MongoDBSettings },
                    { StringsManager.GetString("LibraryPathEditOption") , LibraryPathSettings },
                    { StringsManager.GetString("ListeningURLEditOption"), ChangeListeningURL },
                    { StringsManager.GetString("HTTPSConfigOption"), HTTPSSetup },
                    { StringsManager.GetString("DefaultLanguageOption"), ChangeDeafultLanguage },
                    { StringsManager.GetString("ColorEditOption") , ChangeMelonColors },
                    { StringsManager.GetString("PluginsOption") , ViewPlugins }
                };
        public static void Settings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;

            // Check if settings are loaded and if not load them in. 
            if (StateManager.MelonSettings == null)
            {
                MelonUI.ClearConsole();
                Console.WriteLine(StringsManager.GetString("SettingsLoadingProcess").Pastel(MelonColor.Text));
                if (!Directory.Exists(melonPath))
                {
                    Directory.CreateDirectory(melonPath);
                }

                if (!System.IO.File.Exists($"{melonPath}/Settings.mln"))
                {
                    // If Settings don't exist, add default values.
                    MelonSettings = new Settings()
                    {
                        MongoDbConnectionString = "mongodb://localhost:27017",
                        LibraryPaths = new List<string>()
                    };
                    SaveSettings();
                    DisplayManager.UIExtensions.Add(SetupUI.Display);
                }
                else
                {
                    LoadSettings();
                }
            }


            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption") });

                // Input
                try
                {
                    MenuOptions.Insert(0, StringsManager.GetString("BackNavigation"), () => { LockUI = false; });
                }
                catch (Exception)
                {

                }
                var commands = new List<string>();
                foreach(var key in MenuOptions.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);
                ((Action)MenuOptions[choice])();
            }
        }
        private static void ViewPlugins()
        {
            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("PluginsOption") });
            foreach(var plugin in StateManager.Plugins)
            {
                Console.WriteLine($"{plugin.Name} [{plugin.Version}]");
                Console.WriteLine($" - {plugin.Description}");
                Console.WriteLine($"");
            }
            Console.WriteLine(StringsManager.GetString("ContinuationPrompt"));
            Console.ReadKey();
        }
        private static void HTTPSSetup()
        {
            // Check if ssl is setup already
            var config = Security.GetSSLConfig();
            if(config.Key != "")
            {
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("HTTPSConfigOption") });
                Console.WriteLine(StringsManager.GetString("ServerRestartWarning").Pastel(MelonColor.Highlight));
                Console.WriteLine(StringsManager.GetString("SSLConfigStatus").Pastel(MelonColor.Text));
                var opt = MelonUI.OptionPicker(new List<string>() { StringsManager.GetString("BackNavigation"), StringsManager.GetString("SSLDisableOption"), StringsManager.GetString("SSLConfigEditOption") });
                if(opt == StringsManager.GetString("BackNavigation"))
                {
                    return;
                }
                else if(opt == StringsManager.GetString("SSLDisableOption"))
                {
                    Security.SetSSLConfig("", "");
                    Security.SaveSSLConfig();
                    return;
                }

            }

            bool result = true;
            while (true)
            {
                // Get the Path to the pfx
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("HTTPSConfigOption") });
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
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("HTTPSConfigOption") });
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
                    Security.SaveSSLConfig();
                    break;
                }

            }

        }
        private static void ChangeListeningURL()
        {
            bool result = true;
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("ListeningURLDisplay") });

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

                foreach(var url in input.Split(";"))
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
                    StateManager.SaveSettings();
                    break;
                }

            }

        }
        private static void MongoDBSettings()
        {
            bool check = true;
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("MongoDBOption") });

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
                    SaveSettings();
                    if (DisplayManager.MenuOptions.Count < 5)
                    {
                        DisplayManager.MenuOptions.Clear();
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("FullScanOption"), MelonScanner.Scan);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("ShortScanOption"), MelonScanner.ScanShort);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("DatabaseResetConfirmation"), MelonScanner.ResetDB);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
                        DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));
                    }
                    break;
                }

            }

        }
        private static void ChangeDeafultLanguage()
        {
            bool check = true;
            string input = "";
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("DefaultLanguageOption") });

                // Description
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

                var resources = typeof(Program).Assembly.GetManifestResourceNames();
                if (resources.Contains($"Melon.Strings.UIStrings{input.ToUpper()}.resources"))
                {
                    MelonSettings.DefaultLanguage = input;
                    StringsManager = StringsManager = new ResourceManager($"Melon.Strings.UIStrings{input.ToUpper()}", typeof(Program).Assembly);
                    SaveSettings();
                    DisplayManager.MenuOptions.Clear();
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("FullScanOption"), MelonScanner.Scan);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("ShortScanOption"), MelonScanner.ScanShort);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("DatabaseResetConfirmation"), MelonScanner.ResetDB);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("SettingsOption"), SettingsUI.Settings);
                    DisplayManager.MenuOptions.Add(StringsManager.GetString("ExitOption"), () => Environment.Exit(0));
                    break;
                }
            }

        }
        private static void LibraryPathSettings()
        {
            while (true)
            {
                // title
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("LibraryOption") });
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
                        MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("LibraryOption"), StringsManager.GetString("LibraryAddMenu") });

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
                            StateManager.SaveSettings();
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
                    StateManager.SaveSettings();
                }
            }
        }
        public static void ChangeMelonColors()
        {
            while (true)
            {
                Dictionary<string, int> ColorMenuOptions = new Dictionary<string, int>()
                {
                    { $"Back" , 7 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("NormalTextColorSetting").Pastel(MelonColor.Text)}", 0 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("ShadedTextColorSetting").Pastel(MelonColor.ShadedText)}", 1 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("BackgroundTextColorSetting").Pastel(MelonColor.BackgroundText)}", 2 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("MelonTitleColorSetting").Pastel(MelonColor.Melon)}", 3 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("HighlightColorSetting").Pastel(MelonColor.Highlight)}", 4 },
                    { $"{StringsManager.GetString("ColorSetPromptStart")} {StringsManager.GetString("ErrorColorSetting").Pastel(MelonColor.Error)}", 5 },
                    { StringsManager.GetString("DefaultColorReset"), 6 }
                };
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SettingsOption"), StringsManager.GetString("LibraryOption"), StringsManager.GetString("ColorOptions") });
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
                StateManager.SaveSettings();
            }
        }
    }
}
