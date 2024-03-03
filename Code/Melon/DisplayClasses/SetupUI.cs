using Amazon.Util.Internal;
using Melon.Classes;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.Extensions.Hosting;
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
                MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle"), StringsManager.GetString("SetupProcess") });
                SetupSteps[i]();
            }

            MelonUI.BreadCrumbBar(new List<string>() { StringsManager.GetString("MelonTitle") });
            return;
        }
        private static void ToggleColor()
        {
            Console.WriteLine($"{StringsManager.GetString("WelcomeMessage")} {StringsManager.GetString("MelonTitle").Pastel(MelonColor.Melon)}!".Pastel(MelonColor.Text));
            Console.WriteLine($"{StringsManager.GetString("FirstTimeSetupPrompt")} {StringsManager.GetString("ColorProperty").Pastel(MelonColor.Highlight)} {StringsManager.GetString("MenuColorQuery")}".Pastel(MelonColor.Text));
            Console.WriteLine($"({StringsManager.GetString("ColorVisibilityCheck")})".Pastel(MelonColor.ShadedText));
            MelonSettings.UseMenuColor = false;
            string PositiveConfirmation = StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StringsManager.GetString("NegativeConfirmation");
            string choice = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
            MelonSettings.UseMenuColor = (choice == PositiveConfirmation) ? true : false;
        }
        private static void GetUsername()
        {
            var mongoClient = new MongoClient(MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var UserCollection = mongoDatabase.GetCollection<User>("Users");
            var filter = Builders<User>.Filter.Empty;
            var users = UserCollection.Find(filter);
            if (users.Count() != 0)
            {
                return;
            }

            string PositiveConfirmation = StringsManager.GetString("PositiveConfirmation");
            string NegativeConfirmation = StringsManager.GetString("NegativeConfirmation");
            while (true)
            {
                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine(StringsManager.GetString("SetupUsernamePrompt").Pastel(MelonColor.Text));
                Console.WriteLine($"({StringsManager.GetString("AdminConsideration")} {StringsManager.GetString("AdminAccountDescriptor").Pastel(MelonColor.Highlight)} {StringsManager.GetString("AdminCreationNote")})".Pastel(MelonColor.Text));
                Console.Write("> ".Pastel(MelonColor.Text));
                string nameInput = Console.ReadLine();

                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine(StringsManager.GetString("UsernameConfirmation").Replace("{}", nameInput.Pastel(MelonColor.Highlight)).Pastel(MelonColor.Text));
                string choice = MelonUI.OptionPicker(new List<string>() { PositiveConfirmation, NegativeConfirmation });
                if(choice == PositiveConfirmation)
                {
                    tempUsername = nameInput;
                    break;
                }
            }
        }
        private static void GetPassword()
        {
            var mongoClient = new MongoClient(MelonSettings.MongoDbConnectionString);
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
                Console.WriteLine(StringsManager.GetString("PasswordSetupPrompt").Replace("{}", tempUsername.Pastel(MelonColor.Highlight)).Pastel(MelonColor.Text));
                Console.WriteLine(StringsManager.GetString("AppLoginInstruction").Pastel(MelonColor.Text));
                if (!passInput[0].IsNullOrEmpty())
                {
                    Console.SetCursorPosition(0, 3);
                    Console.WriteLine(StringsManager.GetString("PasswordMismatchError").Pastel(MelonColor.Error));
                }
                Console.WriteLine($"{StringsManager.GetString("PasswordSetting")}: ".Pastel(MelonColor.Text));
                Console.Write($"{StringsManager.GetString("PasswordConfirmation")}: ".Pastel(MelonColor.BackgroundText));
                Console.SetCursorPosition(10, Console.CursorTop - 1);
                passInput[0] = MelonUI.HiddenInput();
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"{StringsManager.GetString("PasswordSetting")}: ".Pastel(MelonColor.BackgroundText));
                Console.Write($"{StringsManager.GetString("PasswordConfirmation")}: ".Pastel(MelonColor.Text));
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
                Console.WriteLine(StringsManager.GetString("MongoDBSetupPrompt").Pastel(MelonColor.Text));
                Console.WriteLine(StringsManager.GetString("ConnectionStringNote").Pastel(MelonColor.Text));
                Console.WriteLine($"({StringsManager.GetString("DefaultMongoDBEntry")}: mongodb://localhost:27017)".Pastel(MelonColor.ShadedText));
                if (showDbError)
                {
                    Console.WriteLine(StringsManager.GetString("MongoDBConnectionError").Pastel(MelonColor.Error));
                    showDbError = false;
                }
                Console.Write("> ");
                string connInput = Console.ReadLine();
                if (connInput == "")
                {
                    MelonSettings.MongoDbConnectionString = "mongodb://localhost:27017";
                    connInput = "mongodb://localhost:27017";
                }
                else
                {
                    MelonSettings.MongoDbConnectionString = connInput;
                }
                MelonUI.ClearConsole(0, 1, Console.WindowWidth, 4);
                Console.WriteLine(StringsManager.GetString("ConnectionCheckStatus").Pastel(MelonColor.Text));
                MelonUI.ShowIndeterminateProgress();
                var check = CheckMongoDB(connInput);
                MelonUI.HideIndeterminateProgress();
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
            Console.WriteLine(StringsManager.GetString("MusicStorageQuery").Pastel(MelonColor.Text));
            Console.WriteLine(StringsManager.GetString("MultiplePathEntry").Pastel(MelonColor.Text));
            bool shownHaHaHaOne = false; // Make sure atleast one path is added
            bool showPathError = false; // Make sure paths are valid
            string pathInput = "";
            while (true)
            {
                // Input UI
                if (showPathError)
                {
                    Console.WriteLine($"{pathInput} {StringsManager.GetString("NotFoundError")}".Pastel(MelonColor.Error));
                    showPathError = false;
                }
                Console.Write($"[{StateManager.MelonSettings.LibraryPaths.Count()}]> ");
                pathInput = Console.ReadLine();

                // If input is "", leave if enough paths are added
                if (pathInput == "")
                {
                    if (StateManager.MelonSettings.LibraryPaths.Count() == 0) // Not enough paths
                    {
                        if (!shownHaHaHaOne)
                        {
                            Console.CursorTop--;
                            Console.WriteLine(StringsManager.GetString("PathRequirementReminder").Pastel(MelonColor.Text));
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
            // Here we'll connect to the database and add the first user
            // We'll also save all the settings set during the OOBE
            var databaseNames = DbClient.ListDatabaseNames().ToList();
            var NewMelonDB = DbClient.GetDatabase("Melon");
            var userCollection = NewMelonDB.GetCollection<User>("Users");
            var metadataCollection = NewMelonDB.GetCollection<DbMetadata>("Metadata");

            var user = new User
            {
                _id = ObjectId.GenerateNewId().ToString(),
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

            var checkMetadata = metadataCollection.AsQueryable().Where(x => x.Name == "UserCollection").FirstOrDefault();
            if(checkMetadata == null)
            {
                var userMetadata = new DbMetadata
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    Name = "UsersCollection",
                    Version = "1.0.0",
                    Info = $""
                };
                metadataCollection.InsertOne(userMetadata);
            }

            userCollection.InsertOne(user);
            Storage.SaveConfigFile("MelonSettings", MelonSettings, new[] { "JWTKey" });
            DisplayManager.UIExtensions.Remove("SetupUI");
            Console.CursorVisible = false;
        }
        public static void ShowSetupError()
        {
            Console.WriteLine($"[!] {StringsManager.GetString("HeadlessSetupRequired")}");
            Console.WriteLine($"[!] {StringsManager.GetString("HeadlessSetupInstructions")}");
        }
    }
}
