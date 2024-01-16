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
using static System.Net.Mime.MediaTypeNames;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// The Out of Box Experience for Melon, gets the users set up from factory defaults.
    /// </summary>
    public static class SetupUI
    {
        private static List<Action> SetupSteps { get; set; }
        private static string tempUsername;
        private static string tempPassword;
        private static byte[] tempSalt;
        public static void Display()
        {
            SetupSteps = new List<Action>()
            {
                ToggleColor,
                MongoDbSetup,
                GetUsername,
                GetPassword,
                GetLibraryPaths,
                CompleteSetup
            };

            for(int i = 0; i < SetupSteps.Count(); i++)
            {
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Setup"});
                SetupSteps[i]();

            }

            MelonUI.BreadCrumbBar(new List<string>() { "Melon" });
            return;
        }
        private static void ToggleColor()
        {
            Console.WriteLine($"Heyo, welcome to {"Melon".Pastel(MelonColor.Melon)}!".Pastel(MelonColor.Text));
            Console.WriteLine($"First thing, would you like to use {"color".Pastel(MelonColor.Highlight)} in menus? ".Pastel(MelonColor.Text));
            Console.WriteLine($"(If you don't see color anywhere now, you should select no.)".Pastel(MelonColor.ShadedText));
            StateManager.MelonSettings.UseMenuColor = false;
            string choice = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
            StateManager.MelonSettings.UseMenuColor = (choice == "Yes") ? true : false;
        }
        private static void GetUsername()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");
            var filter = Builders<User>.Filter.Empty;
            var users = UserCollection.Find(filter);
            if (users.Count() != 0)
            {
                return;
            }

            while (true)
            {
                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine("Let's get you setup, starting with your username.".Pastel(MelonColor.Text));
                Console.WriteLine($"(This will be considered the {"Admin".Pastel(MelonColor.Highlight)} of this Melon instance, and can be changed anytime)".Pastel(MelonColor.Text));
                Console.Write("> ".Pastel(MelonColor.Text));
                string nameInput = Console.ReadLine();

                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine($"Your username is {nameInput.Pastel(MelonColor.Highlight)}, is that right?".Pastel(MelonColor.Text));
                string choice = MelonUI.OptionPicker(new List<string>() { "Yes", "No" });
                if(choice == "Yes")
                {
                    tempUsername = nameInput;
                    break;
                }
            }
        }
        private static void GetPassword()
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");
            var filter = Builders<User>.Filter.Empty;
            var users = UserCollection.Find(filter);
            if (users.Count() != 0)
            {
                return;
            }

            string[] passInput = new string[2];
            do
            {
                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine($"Alright {tempUsername.Pastel(MelonColor.Highlight)}, let's set up a password.".Pastel(MelonColor.Text));
                Console.WriteLine("This will be used to log you in to apps. When you want to add other users, they'll have their own passwords.".Pastel(MelonColor.Text));
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
                if (passInput[0] == "")
                {
                    passInput[0] = new Random().Next().ToString();
                }

            } while (passInput[0] != passInput[1]);
            tempPassword = Security.HashPassword(passInput[0], out tempSalt);

        }
        private static void MongoDbSetup()
        {
            bool showDbError = false;
            while (true)
            {
                Console.WriteLine("Next, we need to connect you to a MongoDb instance.".Pastel(MelonColor.Text));
                Console.WriteLine("This can be local or through atlas, using a connection string".Pastel(MelonColor.Text));
                Console.WriteLine("(Enter nothing for default: mongodb://localhost:27017)".Pastel(MelonColor.ShadedText));
                if (showDbError)
                {
                    Console.WriteLine($"Couldn't connect to that server!".Pastel(MelonColor.Error));
                    showDbError = false;
                }
                Console.Write("> ");
                string connInput = Console.ReadLine();
                if (connInput == "")
                {
                    StateManager.MelonSettings.MongoDbConnectionString = "mongodb://localhost:27017";
                    connInput = "mongodb://localhost:27017";
                }
                else if (connInput.ToLower() == "debug")
                {
                    StateManager.MelonSettings.MongoDbConnectionString = "debug";
                    connInput = "debug";
                    return;
                }
                else
                {
                    StateManager.MelonSettings.MongoDbConnectionString = connInput;
                }
                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine("Checking Connection...".Pastel(MelonColor.Text));
                MelonUI.IndeterminateProgressToggle();
                var check = StateManager.CheckMongoDB(connInput);
                MelonUI.IndeterminateProgressToggle();
                Thread.Sleep(100);
                if (check)
                {
                    break;
                }
                else
                {
                    showDbError = true;
                }
                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
            }
        }
        private static void GetLibraryPaths()
        {
            MelonUI.ClearConsole(0, 1, Console.WindowWidth, 6);
            Console.WriteLine("Lastly, where do you store your music?".Pastel(MelonColor.Text));
            Console.WriteLine("You can enter multiple paths if needed, just enter nothing when you're done.".Pastel(MelonColor.Text));
            bool shownHaHaHaOne = false; // Make sure atleast one path is added
            bool showPathError = false; // Make sure paths are valid
            string pathInput = "";
            while (true)
            {
                // Input UI
                if (showPathError)
                {
                    Console.WriteLine($"{pathInput} was not found!".Pastel(MelonColor.Error));
                    showPathError = false;
                }
                Console.Write($"[{StateManager.MelonSettings.LibraryPaths.Count()}]> ");
                pathInput = Console.ReadLine();

                // If input is "debug" allow passing, maybe turn this into a flag later
                if(pathInput == "debug")
                {
                    break;
                }

                // If input is "", leave if enough paths are added
                if (pathInput == "")
                {
                    if (StateManager.MelonSettings.LibraryPaths.Count() == 0) // Not enough paths
                    {
                        if (!shownHaHaHaOne)
                        {
                            Console.CursorTop--;
                            Console.WriteLine("You're going to need to add at least one path!".Pastel(MelonColor.Text));
                            shownHaHaHaOne = true;
                        }
                        else
                        {
                            Console.CursorLeft = 0;
                            Console.CursorTop--;
                            Console.Write(new string(' ', Console.WindowWidth));
                        }
                    }
                    else
                    {
                        // Leave
                        break;
                    }
                }
                else // Otherwise, check the input for validity
                {
                    if (Directory.Exists(pathInput))
                    {
                        // And Add the path if it is valid
                        StateManager.MelonSettings.LibraryPaths.Add(pathInput);
                    }
                    else
                    {
                        // Or show the red color to indicate invalid path
                        showPathError = true;
                    }
                    Console.CursorLeft = 0;
                    Console.CursorTop--;
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.CursorLeft = 0;
                }
            }
            MelonUI.ClearConsole(0, 1, Console.WindowWidth, 6);
        }
        private static void CompleteSetup()
        { 
            // Don't try to connect if debug was set 
            if(StateManager.MelonSettings.MongoDbConnectionString == "debug")
            {
                StateManager.SaveSettings();
                DisplayManager.UIExtensions.Remove(Display);
                Console.CursorVisible = false;
                return;
            }
            // Here we'll connect to the database and add the first user
            // We'll also save all the settings set during the OOBE
            var databaseNames = StateManager.DbClient.ListDatabaseNames().ToList();
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var collection = NewMelonDB.GetCollection<User>("Users");

            var id = ObjectId.GenerateNewId();
            var document = new User
            {
                _id = new MelonId(id),
                UserId = id.ToString(),
                Username = tempUsername,
                Password = tempPassword,
                Salt = tempSalt,
                LastLogin = DateTime.Now,
                Type = "Admin",
                FavTrack = "",
                FavAlbum = "",
                FavArtist = "",
                PublicStats = false,
                Bio = ""
            };

            collection.InsertOne(document);
            StateManager.SaveSettings();
            DisplayManager.UIExtensions.Remove(Display);
            Console.CursorVisible = false;
        }
    }
}
