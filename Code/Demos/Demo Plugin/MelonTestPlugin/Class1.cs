using Melon.Interface;
using Melon.Models;
using System;
using System.Net.Http;
using System.Windows.Input;

namespace MelonPlugin
{
    public class TestPlugin : IPlugin
    {
        public string Name => "Event Plugin";
        public string Version => "v1.0.0";
        public string Description => "Demo plugin that displays api events";
        public IHost Host { get; set; }
        public bool Display;
        public List<string> Messages;
        public int Execute()
        {
            Host.DisplayManager.MenuOptions.Insert(Host.DisplayManager.MenuOptions.Count - 1, "Events", EventMenu);
            return 0;
        }

        private void EventMenu()
        {
            Display = true;
            Thread t = new Thread(Controller);
            t.Start();
            Messages = new List<string>();
            SetupEventHandlers();
            Host.MelonUI.BreadCrumbBar(new List<string>(){"Melon", "Events"});
            while (Display)
            {
                if(Messages.Count != 0)
                {
                    Console.WriteLine(Messages.First());
                    Messages.RemoveAt(0);
                }
            }
        }

        private void MessageHandler(object sender, WebApiEventArgs e)
        {
            Messages.Add($"[{e.Api}] ({e.User}): {e.Message}");
        }

        private void Controller()
        {
            while (Display)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey();
                    if (k.Key == ConsoleKey.Escape)
                    {
                        Display = false;
                        return;
                    }
                }
            }
        }

        public void LoadMelonCommands(IHost host)
        {
            Host = host;
        }

        public void SetupEventHandlers()
        {
            Host.WebApi.ArtDeleteTrack += MessageHandler;
            Host.WebApi.ArtDeleteAlbum += MessageHandler;
            Host.WebApi.ArtDeleteArtistPfP += MessageHandler;
            Host.WebApi.ArtDeleteArtistBanner += MessageHandler;
            Host.WebApi.ArtDeletePlaylist += MessageHandler;
            Host.WebApi.ArtDeleteCollection += MessageHandler;
            Host.WebApi.ArtDeleteDefault += MessageHandler;

            Host.WebApi.ArtUploadTrack += MessageHandler;
            Host.WebApi.ArtUploadAlbum += MessageHandler;
            Host.WebApi.ArtUploadArtistPfP += MessageHandler;
            Host.WebApi.ArtUploadArtistBanner += MessageHandler;
            Host.WebApi.ArtUploadPlaylist += MessageHandler;
            Host.WebApi.ArtUploadCollection += MessageHandler;
            Host.WebApi.ArtUploadDefault += MessageHandler;

            Host.WebApi.AuthLogin += MessageHandler;
            Host.WebApi.AuthInvite += MessageHandler;
            Host.WebApi.AuthCodeAuthenticate += MessageHandler;

            Host.WebApi.CollectionsCreate += MessageHandler;
            Host.WebApi.CollectionsAddFilters += MessageHandler;
            Host.WebApi.CollectionsRemoveFilters += MessageHandler;
            Host.WebApi.CollectionsDelete += MessageHandler;
            Host.WebApi.CollectionsUpdate += MessageHandler;
            Host.WebApi.CollectionsGet += MessageHandler;
            Host.WebApi.CollectionsSearch += MessageHandler;
            Host.WebApi.CollectionsGetTracks += MessageHandler;

            Host.WebApi.CreateAlbum += MessageHandler;
            Host.WebApi.DeleteAlbum += MessageHandler;
            Host.WebApi.CreateArtist += MessageHandler;
            Host.WebApi.DeleteArtist += MessageHandler;

            Host.WebApi.DbFormat += MessageHandler;
            Host.WebApi.DbBitrate += MessageHandler;
            Host.WebApi.DbSampleRate += MessageHandler;
            Host.WebApi.DbBitsPerSample += MessageHandler;
            Host.WebApi.DbChannel += MessageHandler;
            Host.WebApi.DbReleaseStatus += MessageHandler;
            Host.WebApi.DbReleaseType += MessageHandler;
            Host.WebApi.DbPublisher += MessageHandler;
            Host.WebApi.DbGenres += MessageHandler;

            Host.WebApi.DiscoverTracks += MessageHandler;
            Host.WebApi.DiscoverAlbums += MessageHandler;
            Host.WebApi.DiscoverArtists += MessageHandler;
            Host.WebApi.DiscoverTimeBasedTracks += MessageHandler;

            Host.WebApi.DownloadTrack += MessageHandler;
            Host.WebApi.DownloadTrackTranscode += MessageHandler;
            Host.WebApi.DownloadTrackWave += MessageHandler;
            Host.WebApi.DownloadTrackArt += MessageHandler;
            Host.WebApi.DownloadAlbumArt += MessageHandler;
            Host.WebApi.DownloadArtistPfp += MessageHandler;
            Host.WebApi.DownloadArtistBanner += MessageHandler;
            Host.WebApi.DownloadPlaylistArt += MessageHandler;
            Host.WebApi.DownloadCollectionArt += MessageHandler;

            Host.WebApi.GetTrack += MessageHandler;
            Host.WebApi.GetTracks += MessageHandler;
            Host.WebApi.GetAlbum += MessageHandler;
            Host.WebApi.GetAlbums += MessageHandler;
            Host.WebApi.GetAlbumTracks += MessageHandler;
            Host.WebApi.GetArtist += MessageHandler;
            Host.WebApi.GetArtists += MessageHandler;
            Host.WebApi.GetArtistTracks += MessageHandler;
            Host.WebApi.GetArtistReleases += MessageHandler;
            Host.WebApi.GetArtistSeenOn += MessageHandler;
            Host.WebApi.GetArtistConnections += MessageHandler;
            Host.WebApi.GetLyrics += MessageHandler;

            Host.WebApi.PlaylistsCreate += MessageHandler;
            Host.WebApi.PlaylistsAddTracks += MessageHandler;
            Host.WebApi.PlaylistsRemoveTracks += MessageHandler;
            Host.WebApi.PlaylistsDelete += MessageHandler;
            Host.WebApi.PlaylistsUpdate += MessageHandler;
            Host.WebApi.PlaylistsMoveTrack += MessageHandler;
            Host.WebApi.PlaylistsGet += MessageHandler;
            Host.WebApi.PlaylistsSearch += MessageHandler;
            Host.WebApi.PlaylistsGetTracks += MessageHandler;


            Host.WebApi.QueuesCreateFromTracks += MessageHandler;
            Host.WebApi.QueuesCreateFromAlbums += MessageHandler;
            Host.WebApi.QueuesCreateFromArtists += MessageHandler;
            Host.WebApi.QueuesCreateFromPlaylists += MessageHandler;
            Host.WebApi.QueuesCreateFromCollections += MessageHandler;
            Host.WebApi.QueuesAddTracks += MessageHandler;
            Host.WebApi.QueuesRemoveTracks += MessageHandler;
            Host.WebApi.QueuesDelete += MessageHandler;
            Host.WebApi.QueuesUpdatePosition += MessageHandler;
            Host.WebApi.QueuesUpdate += MessageHandler;
            Host.WebApi.QueuesMoveTrack += MessageHandler;
            Host.WebApi.QueuesGet += MessageHandler;
            Host.WebApi.QueuesSearch += MessageHandler;
            Host.WebApi.QueuesGetTracks += MessageHandler;
            Host.WebApi.QueuesShuffle += MessageHandler;


            Host.WebApi.ScanStart += MessageHandler;
            Host.WebApi.ScanProgress += MessageHandler;


            Host.WebApi.SearchTracks += MessageHandler;
            Host.WebApi.SearchAlbums += MessageHandler;
            Host.WebApi.SearchArtists += MessageHandler;


            Host.WebApi.StatsLogPlay += MessageHandler;
            Host.WebApi.StatsLogSkip += MessageHandler;
            Host.WebApi.StatsListeningTime += MessageHandler;
            Host.WebApi.StatsTopTracks += MessageHandler;
            Host.WebApi.StatsTopAlbums += MessageHandler;
            Host.WebApi.StatsTopArtists += MessageHandler;
            Host.WebApi.StatsTopGenres += MessageHandler;
            Host.WebApi.StatsRecentTrack += MessageHandler;
            Host.WebApi.StatsRecentAlbums += MessageHandler;
            Host.WebApi.StatsRecentArtists += MessageHandler;
            Host.WebApi.StatsRateTrack += MessageHandler;
            Host.WebApi.StatsRateAlbum += MessageHandler;
            Host.WebApi.StatsRateArtist += MessageHandler;


            Host.WebApi.StreamConnect += MessageHandler;
            Host.WebApi.StreamGetExternal += MessageHandler;
            Host.WebApi.StreamPlayExternal += MessageHandler;
            Host.WebApi.StreamPauseExternal += MessageHandler;
            Host.WebApi.StreamSkipExternal += MessageHandler;
            Host.WebApi.StreamRewindExternal += MessageHandler;
            Host.WebApi.StreamVolumeExternal += MessageHandler;


            Host.WebApi.UpdateTrack += MessageHandler;
            Host.WebApi.UpdateAlbum += MessageHandler;
            Host.WebApi.UpdateArtist += MessageHandler;


            Host.WebApi.UsersGet += MessageHandler;
            Host.WebApi.UsersSearch += MessageHandler;
            Host.WebApi.UsersAddFriend += MessageHandler;
            Host.WebApi.UsersRemoveFriend += MessageHandler;
            Host.WebApi.UsersCurrent += MessageHandler;
            Host.WebApi.UsersCreate += MessageHandler;
            Host.WebApi.UsersDelete += MessageHandler;
            Host.WebApi.UsersUpdate += MessageHandler;
            Host.WebApi.UsersChangeUsername += MessageHandler;
            Host.WebApi.UsersChangePassword += MessageHandler;
        }
    }
}