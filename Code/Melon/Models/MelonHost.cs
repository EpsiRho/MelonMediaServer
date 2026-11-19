using Melon.LocalClasses;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Melon.Models;
using Melon.Interface;
using Melon.Types;

namespace Melon.PluginModels
{
    public class MelonHost : IHost
    {
        public string Version => "1.2.0";
        public IMelonAPI Api => new API();
        public IMelonAPI MelonAPI => new API();
        public IStorageAPI Storage => new StorageAPI();
        public IMelonScanner MelonScanner => new Scanner();
        public IStateManager StateManager => new State();
        public IMelonAdministration Administration => ServerAdministration.Instance;
    }
    public class API : IMelonAPI
    {
        public List<Track> ShuffleTracks(List<Track> tracks, string UserId, ShuffleType type, bool fullRandom = false, bool enableTrackLinks = true)
        {
            return MelonAPI.ShuffleTracks(tracks, UserId, type, fullRandom, enableTrackLinks);
        }
    }
    public class StorageAPI : IStorageAPI
    {
        public T LoadConfigFile<T>(string filename, string[] protectedProperties, out bool converted)
        {
            return Storage.LoadConfigFile<T>(filename, protectedProperties, out converted);
        }
        public void SaveConfigFile<T>(string filename, T config, string[] protectedProperties)
        {
            Storage.SaveConfigFile(filename, config, protectedProperties);
        }
    }
    public class Scanner : IMelonScanner
    {
        public string CurrentFolder => MelonScanner.CurrentFolder;
        public string CurrentFile => MelonScanner.CurrentFile;
        public string CurrentStatus => MelonScanner.CurrentStatus;
        public double ScannedFiles => MelonScanner.ScannedFiles;
        public double FoundFiles => MelonScanner.FoundFiles;
        public long averageMilliseconds => MelonScanner.averageMilliseconds;
        public bool Indexed => MelonScanner.Indexed;
        public bool Scanning => MelonScanner.Scanning;
        public void StartScan(bool skip)
        {
            ServerAdministration.Instance.StartScan(skip);
        }
        public void UpdateCollections()
        {
            MelonScanner.UpdateCollections();
        }
        public void ResetDB()
        {
            ServerAdministration.Instance.ResetDatabase();
        }
    }
    public class State : IStateManager
    {
        public string melonPath
        {
            get
            {
                return StateManager.melonPath;
            }
        }
        public MongoClient DbClient
        {
            get
            {
                return StateManager.DbClient;
            }
        }
        public ShortSettings MelonSettings
        {
            get
            {
                return new ShortSettings(StateManager.MelonSettings);
            }
        }
        public Flags MelonFlags
        {
            get
            {
                return StateManager.MelonFlags;
            }
        }
        public ResourceManager StringsManager
        {
            get
            {
                return StateManager.StringsManager;
            }
        }
        public List<IPlugin> Plugins
        {
            get
            {
                return [.. StateManager.Plugins];
            }
        }
        public Dictionary<string, string> LaunchArgs
        {
            get
            {
                return StateManager.LaunchArgs;
            }
        }

        public byte[] GetDefaultImage()
        {
            return StateManager.GetDefaultImage();
        }
    }
}
