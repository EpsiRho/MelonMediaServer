using Melon.Models;
using Melon.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Interface
{
    public interface IHost
    {
        public string Version { get; }
        IMelonAPI MelonAPI { get; }
        IStorageAPI Storage { get; }
        IMelonScanner MelonScanner { get; }
        IStateManager StateManager { get; }
        IDisplayManager DisplayManager { get; }
        IMelonUI MelonUI { get; }
        ISettingsUI SettingsUI { get; }
        IWebApi WebApi { get; set; }
    }
    public interface IMelonAPI
    {
        public List<Track> ShuffleTracks(List<Track> tracks, string UserId, ShuffleType type, bool fullRandom = false, bool enableTrackLinks = true);
    }
    public interface IStorageAPI
    {
        public T LoadConfigFile<T>(string filename, string[] protectedProperties);
        public void SaveConfigFile<T>(string filename, T config, string[] protectedProperties);
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
        public void Sort();
    }
    public interface IStateManager
    {
        public string melonPath { get; }
        public ShortSettings MelonSettings { get; }
        public Flags MelonFlags { get; }
        public ResourceManager StringsManager { get; }
        public List<IPlugin> Plugins { get; }
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
        public void ShowIndeterminateProgress();
        public void HideIndeterminateProgress();
        public string OptionPicker(List<string> Choices);
        public string StringInput(bool UsePred, bool AutoCorrect, bool FreeInput, bool ShowChoices, List<string> Choices = null);
        public void ChecklistDisplayToggle();
        public void SetChecklistItems(string[] list);
        public void InsertInChecklist(string item, int place, bool check);
        public void UpdateChecklist(int place, bool check);
    }
    public interface ISettingsUI
    {
        public OrderedDictionary MenuOptions { get; set; }
    }
    public interface IWebApi
    {
        // Middleware
        public bool UsePluginMiddleware(KeyValuePair<string, Func<WebApiEventArgs, byte[]>> middleware);
        public bool RemovePluginMiddleware(string name);
        public Dictionary<string, Func<WebApiEventArgs, byte[]>> GetPluginMiddlewares();

        // ArtDeleteController
        public event EventHandler<WebApiEventArgs> ArtDeleteTrack;
        public event EventHandler<WebApiEventArgs> ArtDeleteAlbum;
        public event EventHandler<WebApiEventArgs> ArtDeleteArtistPfP;
        public event EventHandler<WebApiEventArgs> ArtDeleteArtistBanner;
        public event EventHandler<WebApiEventArgs> ArtDeletePlaylist;
        public event EventHandler<WebApiEventArgs> ArtDeleteCollection;
        public event EventHandler<WebApiEventArgs> ArtDeleteDefault;

        // ArtUploadController
        public event EventHandler<WebApiEventArgs> ArtUploadTrack;
        public event EventHandler<WebApiEventArgs> ArtUploadAlbum;
        public event EventHandler<WebApiEventArgs> ArtUploadArtistPfP;
        public event EventHandler<WebApiEventArgs> ArtUploadArtistBanner;
        public event EventHandler<WebApiEventArgs> ArtUploadPlaylist;
        public event EventHandler<WebApiEventArgs> ArtUploadCollection;
        public event EventHandler<WebApiEventArgs> ArtUploadDefault;

        // AuthController
        public event EventHandler<WebApiEventArgs> AuthLogin;
        public event EventHandler<WebApiEventArgs> AuthInvite;
        public event EventHandler<WebApiEventArgs> AuthCodeAuthenticate;

        // CollectionsController
        public event EventHandler<WebApiEventArgs> CollectionsCreate;
        public event EventHandler<WebApiEventArgs> CollectionsAddFilters;
        public event EventHandler<WebApiEventArgs> CollectionsRemoveFilters;
        public event EventHandler<WebApiEventArgs> CollectionsDelete;
        public event EventHandler<WebApiEventArgs> CollectionsUpdate;
        public event EventHandler<WebApiEventArgs> CollectionsGet;
        public event EventHandler<WebApiEventArgs> CollectionsSearch;
        public event EventHandler<WebApiEventArgs> CollectionsGetTracks;

        // CreateDeleteController
        public event EventHandler<WebApiEventArgs> CreateAlbum;
        public event EventHandler<WebApiEventArgs> DeleteAlbum;
        public event EventHandler<WebApiEventArgs> CreateArtist;
        public event EventHandler<WebApiEventArgs> DeleteArtist;

        // DatabaseController
        public event EventHandler<WebApiEventArgs> DbFormat;
        public event EventHandler<WebApiEventArgs> DbBitrate;
        public event EventHandler<WebApiEventArgs> DbSampleRate;
        public event EventHandler<WebApiEventArgs> DbBitsPerSample;
        public event EventHandler<WebApiEventArgs> DbChannel;
        public event EventHandler<WebApiEventArgs> DbReleaseStatus;
        public event EventHandler<WebApiEventArgs> DbReleaseType;
        public event EventHandler<WebApiEventArgs> DbPublisher;
        public event EventHandler<WebApiEventArgs> DbGenres;

        // DiscoverController
        public event EventHandler<WebApiEventArgs> DiscoverTracks;
        public event EventHandler<WebApiEventArgs> DiscoverAlbums;
        public event EventHandler<WebApiEventArgs> DiscoverArtists;
        public event EventHandler<WebApiEventArgs> DiscoverTimeBasedTracks;

        // DownloadController
        public event EventHandler<WebApiEventArgs> DownloadTrack;
        public event EventHandler<WebApiEventArgs> DownloadTrackTranscode;
        public event EventHandler<WebApiEventArgs> DownloadTrackWave;
        public event EventHandler<WebApiEventArgs> DownloadTrackArt;
        public event EventHandler<WebApiEventArgs> DownloadAlbumArt;
        public event EventHandler<WebApiEventArgs> DownloadArtistPfp;
        public event EventHandler<WebApiEventArgs> DownloadArtistBanner;
        public event EventHandler<WebApiEventArgs> DownloadPlaylistArt;
        public event EventHandler<WebApiEventArgs> DownloadCollectionArt;

        // GeneralController
        public event EventHandler<WebApiEventArgs> GetTrack;
        public event EventHandler<WebApiEventArgs> GetTracks;
        public event EventHandler<WebApiEventArgs> GetAlbum;
        public event EventHandler<WebApiEventArgs> GetAlbums;
        public event EventHandler<WebApiEventArgs> GetAlbumTracks;
        public event EventHandler<WebApiEventArgs> GetArtist;
        public event EventHandler<WebApiEventArgs> GetArtists;
        public event EventHandler<WebApiEventArgs> GetArtistTracks;
        public event EventHandler<WebApiEventArgs> GetArtistReleases;
        public event EventHandler<WebApiEventArgs> GetArtistSeenOn;
        public event EventHandler<WebApiEventArgs> GetArtistConnections;
        public event EventHandler<WebApiEventArgs> GetLyrics;

        // PlaylistsController
        public event EventHandler<WebApiEventArgs> PlaylistsCreate;
        public event EventHandler<WebApiEventArgs> PlaylistsAddTracks;
        public event EventHandler<WebApiEventArgs> PlaylistsRemoveTracks;
        public event EventHandler<WebApiEventArgs> PlaylistsDelete;
        public event EventHandler<WebApiEventArgs> PlaylistsUpdate;
        public event EventHandler<WebApiEventArgs> PlaylistsMoveTrack;
        public event EventHandler<WebApiEventArgs> PlaylistsGet;
        public event EventHandler<WebApiEventArgs> PlaylistsSearch;
        public event EventHandler<WebApiEventArgs> PlaylistsGetTracks;

        // QueuesController
        public event EventHandler<WebApiEventArgs> QueuesCreateFromTracks;
        public event EventHandler<WebApiEventArgs> QueuesCreateFromAlbums;
        public event EventHandler<WebApiEventArgs> QueuesCreateFromArtists;
        public event EventHandler<WebApiEventArgs> QueuesCreateFromPlaylists;
        public event EventHandler<WebApiEventArgs> QueuesCreateFromCollections;
        public event EventHandler<WebApiEventArgs> QueuesAddTracks;
        public event EventHandler<WebApiEventArgs> QueuesRemoveTracks;
        public event EventHandler<WebApiEventArgs> QueuesDelete;
        public event EventHandler<WebApiEventArgs> QueuesUpdatePosition;
        public event EventHandler<WebApiEventArgs> QueuesUpdate;
        public event EventHandler<WebApiEventArgs> QueuesMoveTrack;
        public event EventHandler<WebApiEventArgs> QueuesGet;
        public event EventHandler<WebApiEventArgs> QueuesSearch;
        public event EventHandler<WebApiEventArgs> QueuesGetTracks;
        public event EventHandler<WebApiEventArgs> QueuesShuffle;

        // ScanController
        public event EventHandler<WebApiEventArgs> ScanStart;
        public event EventHandler<WebApiEventArgs> ScanProgress;

        // SearchController
        public event EventHandler<WebApiEventArgs> SearchTracks;
        public event EventHandler<WebApiEventArgs> SearchAlbums;
        public event EventHandler<WebApiEventArgs> SearchArtists;

        // StatsController
        public event EventHandler<WebApiEventArgs> StatsLogPlay;
        public event EventHandler<WebApiEventArgs> StatsLogSkip;
        public event EventHandler<WebApiEventArgs> StatsListeningTime;
        public event EventHandler<WebApiEventArgs> StatsTopTracks;
        public event EventHandler<WebApiEventArgs> StatsTopAlbums;
        public event EventHandler<WebApiEventArgs> StatsTopArtists;
        public event EventHandler<WebApiEventArgs> StatsTopGenres;
        public event EventHandler<WebApiEventArgs> StatsRecentTrack;
        public event EventHandler<WebApiEventArgs> StatsRecentAlbums;
        public event EventHandler<WebApiEventArgs> StatsRecentArtists;
        public event EventHandler<WebApiEventArgs> StatsRateTrack;
        public event EventHandler<WebApiEventArgs> StatsRateAlbum;
        public event EventHandler<WebApiEventArgs> StatsRateArtist;

        // StreamController
        public event EventHandler<WebApiEventArgs> StreamConnect;
        public event EventHandler<WebApiEventArgs> StreamGetExternal;
        public event EventHandler<WebApiEventArgs> StreamPlayExternal;
        public event EventHandler<WebApiEventArgs> StreamPauseExternal;
        public event EventHandler<WebApiEventArgs> StreamSkipExternal;
        public event EventHandler<WebApiEventArgs> StreamRewindExternal;
        public event EventHandler<WebApiEventArgs> StreamVolumeExternal;

        // UpdateController
        public event EventHandler<WebApiEventArgs> UpdateTrack;
        public event EventHandler<WebApiEventArgs> UpdateAlbum;
        public event EventHandler<WebApiEventArgs> UpdateArtist;

        // UserController
        public event EventHandler<WebApiEventArgs> UsersGet;
        public event EventHandler<WebApiEventArgs> UsersSearch;
        public event EventHandler<WebApiEventArgs> UsersAddFriend;
        public event EventHandler<WebApiEventArgs> UsersRemoveFriend;
        public event EventHandler<WebApiEventArgs> UsersCurrent;
        public event EventHandler<WebApiEventArgs> UsersCreate;
        public event EventHandler<WebApiEventArgs> UsersDelete;
        public event EventHandler<WebApiEventArgs> UsersUpdate;
        public event EventHandler<WebApiEventArgs> UsersChangeUsername;
        public event EventHandler<WebApiEventArgs> UsersChangePassword;
    }
    
}
