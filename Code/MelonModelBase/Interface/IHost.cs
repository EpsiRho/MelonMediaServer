using Melon.Models;
using Melon.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Interface
{
    public interface IHost
    {
        public string Version { get; }
        IMelonAPI MelonAPI { get; }
        IMelonScanner MelonScanner { get; }
        IStateManager StateManager { get; }
        IDisplayManager DisplayManager { get; }
        IMelonUI MelonUI { get; }
        ISettingsUI SettingsUI { get; }
    }
    public interface IMelonAPI
    {
        public List<Track> ShuffleTracks(List<Track> tracks, string UserId, ShuffleType type, bool fullRandom = false, bool enableTrackLinks = true);
    }
    public interface IMelonScanner
    {
        public string CurrentFolder { get; set; }
        public string CurrentFile { get; set; }
        public string CurrentStatus { get; set; }
        public double ScannedFiles { get; set; }
        public double FoundFiles { get; set; }
        public long averageMilliseconds { get; set; }
        public bool Indexed { get; set; }
        public bool endDisplay { get; set; }
        public bool Scanning { get; set; }
        public void StartScan(bool skip);
        public void UpdateCollections();
        public void ResetDB();
        public void Scan();
        public void ScanShort();
        public void ScanProgressView();
    }
    public interface IStateManager
    {
        public string melonPath { get; }
        public Settings MelonSettings { get; }
        public Flags MelonFlags { get; }
        public ResourceManager StringsManager { get; }
        public byte[] GetDefaultImage();
    }
    public interface IDisplayManager
    {
        public OrderedDictionary MenuOptions { get; set; }
        public List<Action> UIExtensions { get; set; }
    }
    public interface IMelonUI
    {
        public void BreadCrumbBar(List<string> list);
        public void ClearConsole();
        public void ClearConsole(int left, int top, int width, int height);
        public Color ColorPicker(Color CurColor);
        public string HiddenInput();
        public void DisplayProgressBar(double count, double max, char foreground, char background);
        public void IndeterminateProgressToggle();
        public string OptionPicker(List<string> Choices);
        public string StringInput(bool UsePred, bool AutoCorrect, bool FreeInput, bool ShowChoices, List<string> Choices = null);
    }
    public interface ISettingsUI
    {
        public OrderedDictionary MenuOptions { get; set; }
    }
    
}
