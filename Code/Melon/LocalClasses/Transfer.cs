using Melon.Models;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melon.LocalClasses;
using Melon.Classes;
namespace Melon.LocalClasses
{
    public static class Transfer
    {
        public static void TransferUI()
        {
            // Used to stay in settings until back is selected
            bool LockUI = true;
            var options = new OrderedDictionary()
                {
                    { StateManager.StringsManager.GetString("BackNavigation"), () => { LockUI = false; } },
                    { "Export Database", Export },
                    { "Import Database", Import },
                    { "Export Playlist", Import },
                    { "Import Playlist", Import },
                };

            while (LockUI)
            {
                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle"), StateManager.StringsManager.GetString("SettingsOption") });

                // Input
                var commands = new List<string>();
                foreach (var key in options.Keys)
                {
                    commands.Add((string)key);
                }
                var choice = MelonUI.OptionPicker(commands);

                if (choice == StateManager.StringsManager.GetString("BackNavigation"))
                {
                    LockUI = false;
                    break;
                }

                ((Action)options[choice])();
            }
        }
        public static void Export()
        {
            var NewMelonDB = StateManager.DbClient.GetDatabase("Melon");
            var TracksCollection = NewMelonDB.GetCollection<Track>("Tracks");
            var ArtistCollection = NewMelonDB.GetCollection<Artist>("Artists");
            var AlbumCollection = NewMelonDB.GetCollection<Album>("Albums");
            var QueueCollection = NewMelonDB.GetCollection<PlayQueue>("Queues");
            var PlaylistCollection = NewMelonDB.GetCollection<Playlist>("Playlists");
            var collectionCollection = NewMelonDB.GetCollection<Collection>("Collections");
            var failedCollection = NewMelonDB.GetCollection<FailedFile>("FailedFiles");
            var metadataCollection = NewMelonDB.GetCollection<DbMetadata>("Metadata");
            var statsCollection = NewMelonDB.GetCollection<PlayStat>("Stats");

            List<string> lines = new List<string>();

            string json = JsonConvert.SerializeObject(TracksCollection.AsQueryable());

            foreach(var track in TracksCollection.AsQueryable())
            {

            }

            if (!Directory.Exists($"{StateManager.melonPath}/Exports"))
            {
                Directory.CreateDirectory($"{StateManager.melonPath}/Exports");
            }
            File.WriteAllText($"{StateManager.melonPath}/Exports/{DateTime.Now.ToString("ddmmyyyy-hhmmss")}-tracks.json", json);
        }
        public static void Import()
        {

        }
    }
}
