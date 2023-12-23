using Melon.Classes;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Melon.LocalClasses.StateManager;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// Contains all the UI for adjusting settings.
    /// </summary>
    public static class SettingsUI
    {
        public static void Settings()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;

            // Check if settings are loaded and if not load them in. 
            if (StateManager.MelonSettings == null)
            {
                MelonUI.ClearConsole();
                Console.WriteLine("Loading Settings...".Pastel(MelonColor.Text));
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
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings" });

                // Input
                Dictionary<string, Action> MenuOptions = new Dictionary<string, Action>()
                {
                    { "Back" , () => { LockUI = false; } },
                    { "Edit Users", UserSettings },
                    { "Edit MongoDB Connection", MongoDBSettings },
                    { "Edit Library Paths" , LibraryPathSettings },
                    { "Edit Colors " , ChangeMelonColors }
                };
                var choice = MelonUI.OptionPicker(MenuOptions.Keys.ToList());
                MenuOptions[choice]();
            }
        }
        private static void UserSettings()
        {
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users" });

                // Get Users from MelonDb
                var databaseNames = DbClient.ListDatabaseNames().ToList();
                var NewMelonDB = DbClient.GetDatabase("Melon");
                var collection = NewMelonDB.GetCollection<User>("Users");
                var documents = collection.Find<User>(Builders<User>.Filter.Empty).ToList();

                List<string> users = new List<string>();
                List<string> names = new List<string>();
                users.Add("Back");
                users.Add("Add New User");
                foreach (var doc in documents)
                {
                    names.Add(doc.Username);
                    users.Add($"[{doc.Type}] {doc.Username} ({DateTime.Parse(doc.LastLogin.ToString("MM/dd/yyyy hh:mm tt"))})");
                }

                // Add Extra Options

                // Option Picker for selection
                string input = MelonUI.OptionPicker(users);
                if (input == "Add New User")
                {
                    string username = "";
                    while (true)
                    {
                        // Title
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users", "Add User 1/3" });
                        Console.WriteLine("Enter the new user's name (Must be unique):".Pastel(MelonColor.Text));
                        Console.Write($"> ".Pastel(MelonColor.Text));

                        // Get User Name
                        username = Console.ReadLine();
                        if (!names.Contains(username))
                        {
                            break;
                        }

                    }

                    // Get Password
                    string[] passInput = new string[2];
                    do
                    {
                        MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users", "Add User 2/3" });
                        Console.WriteLine("Please enter a password for this user".Pastel(MelonColor.Text));
                        if (!passInput[0].IsNullOrEmpty())
                        {
                            Console.SetCursorPosition(0, 3);
                            Console.WriteLine("Passwords do not match, please try again.".Pastel(MelonColor.Error));
                        }
                        Console.WriteLine("Password: ".Pastel(MelonColor.Text));
                        Console.Write("Confirm password: ".Pastel(MelonColor.BackgroundText));
                        Console.SetCursorPosition(10, Console.CursorTop - 1);
                        passInput[0] = MelonUI.HiddenInput();
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.WriteLine("Password: ".Pastel(MelonColor.BackgroundText));
                        Console.Write("Confirm password: ".Pastel(MelonColor.Text));
                        passInput[1] = MelonUI.HiddenInput();

                    } while (passInput[0] != passInput[1]);

                    // Get User Type
                    MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users", "Add User 3/3" });
                    Console.WriteLine("Select the new user's type".Pastel(MelonColor.Text));
                    var type = MelonUI.OptionPicker(new List<string>()
                    {
                        "Admin",
                        "User",
                        "Pass"
                    });

                    byte[] tempSalt;
                    var password = Security.HashPasword(passInput[0], out tempSalt);
                    var id = ObjectId.GenerateNewId();

                    var document = new User
                    {
                        _id = id,
                        UserId = id.ToString(),
                        Username = username,
                        Password = password,
                        Salt = tempSalt,
                        LastLogin = DateTime.Now,
                        Type = "Admin"
                    };
                    collection.InsertOne(document);
                }
                else if (input == "Back")
                {
                    // Leave
                    return;
                }
                else
                {
                    // Delete User
                    //int idx = users.IndexOf(input);
                    //User item = documents[idx];
                    //var username = item.Username;
                    //var deleteFilter = Builders<User>.Filter.Eq("Username", username);
                    //collection.DeleteOne(deleteFilter);
                    MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users", "Password Check" });
                    Console.WriteLine("Enter the user's password:".Pastel(MelonColor.Text));
                    Console.Write($"> ".Pastel(MelonColor.Text));
                    var pass = Console.ReadLine();
                    int idx = users.IndexOf(input);
                    User user = documents[idx-2];
                    bool Auth = Security.VerifyPassword(pass, user.Password, user.Salt);
                    if(Auth)
                    {
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users", "Password Check" });
                        Console.WriteLine("The password was correct!".Pastel(MelonColor.Text));
                        Console.ReadLine();
                    }
                    else
                    {
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Users", "Password Check" });
                        Console.WriteLine("The password was incorrect!".Pastel(MelonColor.Text));
                        Console.ReadLine();
                    }
                }
            }
        }
        private static void MongoDBSettings()
        {
            bool check = true;
            while (true)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "MongoDB" });

                // Description
                Console.WriteLine($"Current MongoDB connection string: {StateManager.MelonSettings.MongoDbConnectionString.Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
                Console.WriteLine($"(Enter a new string or nothing to keep the current string)".Pastel(MelonColor.Text));
                if (!check)
                {
                    Console.WriteLine($"[Couldn't connect to server, try again]".Pastel(MelonColor.Error));
                }
                check = false;

                // Get New MongoDb Connection String
                Console.Write("> ".Pastel(MelonColor.Text));
                string input = Console.ReadLine();
                if (input == "")
                {
                    return;
                }

                check = StateManager.CheckMongoDB(input);
                if (check)
                {
                    // Set and Save new conn string
                    StateManager.MelonSettings.MongoDbConnectionString = input;
                    StateManager.SaveSettings();
                    if (DisplayManager.MenuOptions.Count < 5)
                    {
                        DisplayManager.MenuOptions.Clear();
                        DisplayManager.MenuOptions.Add("Full Scan", MelonScanner.Scan);
                        DisplayManager.MenuOptions.Add("Short Scan", MelonScanner.ScanShort);
                        DisplayManager.MenuOptions.Add("Reset DB", MelonScanner.ResetDB);
                        DisplayManager.MenuOptions.Add("Settings", SettingsUI.Settings);
                        DisplayManager.MenuOptions.Add("Exit", () => Environment.Exit(0));
                    }
                    break;
                }

            }

        }
        private static void LibraryPathSettings()
        {
            while (true)
            {
                // title
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Libraries" });
                Console.WriteLine($"(Select a path to delete it)".Pastel(MelonColor.Text));

                // Get paths
                List<string> NewPaths = new List<string>();
                NewPaths.Add("Back");
                NewPaths.Add("Add New Path");
                NewPaths.AddRange(StateManager.MelonSettings.LibraryPaths);

                // Add Options

                // Get Selection
                string input = MelonUI.OptionPicker(NewPaths);
                if (input == "Add New Path")
                {
                    // For showing error color when directory doesn't exist
                    bool showPathError = false;
                    while (true)
                    {
                        // Title
                        MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Libraries", "Add Library" });

                        // Description and Input UI
                        if (showPathError)
                        {
                            Console.WriteLine("Invalid Path, Please try again (Or enter nothing to quit)".Pastel(MelonColor.Error));
                            showPathError = false;
                        }
                        Console.WriteLine("Enter a new library path:".Pastel(MelonColor.Text));
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
                else if (input == "Back")
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
                    { $"Set the {"normal text color".Pastel(MelonColor.Text)}", 0 },
                    { $"Set the {"shaded text color".Pastel(MelonColor.ShadedText)}", 1 },
                    { $"Set the {"background text color".Pastel(MelonColor.BackgroundText)}", 2 },
                    { $"Set the {"Melon Title/Select color".Pastel(MelonColor.Melon)}", 3 },
                    { $"Set the {"highlight color".Pastel(MelonColor.Highlight)}", 4 },
                    { $"Set the {"error color".Pastel(MelonColor.Error)}", 5 },
                    { $"Set all colors back to their defaults", 6 }
                };
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Settings", "Colors" });
                Console.WriteLine("Choose a color to change:".Pastel(MelonColor.Text));
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
