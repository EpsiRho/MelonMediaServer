using Melon.Classes;
using Melon.DisplayClasses;
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
        public IMelonAPI Api => new API();

        public string Version => "1.0.0";

        public IMelonAPI MelonAPI => new API();

        public IMelonScanner MelonScanner => new Scanner();

        public IStateManager StateManager => new State();

        public IDisplayManager DisplayManager => new Display();

        public IMelonUI MelonUI => new UI();

        public ISettingsUI SettingsUI => new SettingsMenu();
        private IWebApi _WebApi;
        public IWebApi WebApi 
        {
            get
            {
                return _WebApi;
            }
            set
            {
                if (_WebApi != value)
                {
                    _WebApi = value;
                }
            }
        }
    }
    public class API : IMelonAPI
    {
        public List<Track> ShuffleTracks(List<Track> tracks, string UserId, ShuffleType type, bool fullRandom = false, bool enableTrackLinks = true)
        {
            return MelonAPI.ShuffleTracks(tracks, UserId, type, fullRandom, enableTrackLinks);
        }
    }
    public class Scanner : IMelonScanner
    {
        public string CurrentFolder
        {
            get
            {
                return MelonScanner.CurrentFolder;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.CurrentFolder = value;
                }
            }
        }
        public string CurrentFile
        {
            get
            {
                return MelonScanner.CurrentFile;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.CurrentFile = value;
                }
            }
        }
        public string CurrentStatus
        {
            get
            {
                return MelonScanner.CurrentStatus;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.CurrentStatus = value;
                }
            }
        }
        public double ScannedFiles
        {
            get
            {
                return MelonScanner.ScannedFiles;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.ScannedFiles = value;
                }
            }
        }
        public double FoundFiles
        {
            get
            {
                return MelonScanner.FoundFiles;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.FoundFiles = value;
                }
            }
        }
        public long averageMilliseconds
        {
            get
            {
                return MelonScanner.averageMilliseconds;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.averageMilliseconds = value;
                }
            }
        }
        public bool Indexed
        {
            get
            {
                return MelonScanner.Indexed;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.Indexed = value;
                }
            }
        }
        public bool endDisplay
        {
            get
            {
                return MelonScanner.endDisplay;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.endDisplay = value;
                }
            }
        }
        public bool Scanning
        {
            get
            {
                return MelonScanner.Scanning;
            }
            set
            {
                if (value == null)
                {
                    MelonScanner.Scanning = value;
                }
            }
        }
        public void StartScan(bool skip)
        {
            MelonScanner.StartScan(skip);
        }
        public void UpdateCollections()
        {
            MelonScanner.UpdateCollections();
        }
        public void ResetDB()
        {
            MelonScanner.ResetDB();
        }
        public void Scan()
        {
            MelonScanner.Scan();
        }
        public void ScanShort()
        {
            MelonScanner.ScanShort();
        }
        public void ScanProgressView()
        {
            MelonScanner.ScanProgressView();
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
        public Settings MelonSettings
        {
            get
            {
                return StateManager.MelonSettings;
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
                return StateManager.Plugins;
            }
        }

        public byte[] GetDefaultImage()
        {
            return StateManager.GetDefaultImage();
        }
    }
    public class Display : IDisplayManager
    {
        public OrderedDictionary MenuOptions
        {
            get
            {
                return DisplayManager.MenuOptions;
            }
            set
            {
                if (value == null)
                {
                    DisplayManager.MenuOptions = value;
                }
            }
        }
        public List<Action> UIExtensions
        {
            get
            {
                return DisplayManager.UIExtensions;
            }
            set
            {
                if (value == null)
                {
                    DisplayManager.UIExtensions = value;
                }
            }
        }
    }
    public class UI : IMelonUI
    {
        public void BreadCrumbBar(List<string> list)
        {
            MelonUI.BreadCrumbBar(list);
        }
        public void ClearConsole()
        {
            MelonUI.ClearConsole();
        }
        public void ClearConsole(int left, int top, int width, int height)
        {
            MelonUI.ClearConsole(left, top, width, height);
        }
        public Color ColorPicker(Color CurColor)
        {
            return MelonUI.ColorPicker(CurColor);
        }
        public string HiddenInput()
        {
            return MelonUI.HiddenInput();
        }
        public void DisplayProgressBar(double count, double max, char foreground, char background)
        {
            MelonUI.DisplayProgressBar(count, max, foreground, background);
        }
        public void IndeterminateProgressToggle()
        {
            MelonUI.IndeterminateProgressToggle();
        }
        public string OptionPicker(List<string> Choices)
        {
            return MelonUI.OptionPicker(Choices);
        }
        public string StringInput(bool UsePred, bool AutoCorrect, bool FreeInput, bool ShowChoices, List<string> Choices = null)
        {
            return MelonUI.StringInput(UsePred, AutoCorrect, FreeInput, ShowChoices, Choices);
        }
    }
    public class SettingsMenu : ISettingsUI
    {
        public OrderedDictionary MenuOptions
        {
            get
            {
                return SettingsUI.MenuOptions;
            }
            set
            {
                if (value == null)
                {
                    SettingsUI.MenuOptions = value;
                }
            }
        }
    }
}
