using Melon.Interface;

namespace Melon.Models
{
    public class MWebApi : IWebApi
    {
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

        public void OnApiCall(WebApiEventArgs args)
        {
            switch (args.Api)
            {
                // ArtDeleteController
                case "api/art/delete/track-art":
                    ArtDeleteTrack?.Invoke(this, args);
                    break;
                case "api/art/delete/album-art":
                    ArtDeleteAlbum?.Invoke(this, args);
                    break;
                case "api/art/delete/artist-pfp":
                    ArtDeleteArtistPfP?.Invoke(this, args);
                    break;
                case "api/art/delete/artist-banner":
                    ArtDeleteArtistBanner?.Invoke(this, args);
                    break;
                case "api/art/delete/playlist-art":
                    ArtDeletePlaylist?.Invoke(this, args);
                    break;
                case "api/art/delete/collection-art":
                    ArtDeleteCollection?.Invoke(this, args);
                    break;
                case "api/art/delete/default-art":
                    ArtDeleteDefault?.Invoke(this, args);
                    break;

                // ArtUploadController
                case "api/art/upload/track-art":
                    ArtUploadTrack?.Invoke(this, args);
                    break;
                case "api/art/upload/album-art":
                    ArtUploadAlbum?.Invoke(this, args);
                    break;
                case "api/art/upload/artist-pfp":
                    ArtUploadArtistPfP?.Invoke(this, args);
                    break;
                case "api/art/upload/artist-banner":
                    ArtUploadArtistBanner?.Invoke(this, args);
                    break;
                case "api/art/upload/playlist-art":
                    ArtUploadPlaylist?.Invoke(this, args);
                    break;
                case "api/art/upload/collection-art":
                    ArtUploadCollection?.Invoke(this, args);
                    break;
                case "api/art/upload/default-art":
                    ArtUploadDefault?.Invoke(this, args);
                    break;

                // AuthController
                case "auth/login":
                    AuthLogin?.Invoke(this, args);
                    break;
                case "auth/invite":
                    AuthInvite?.Invoke(this, args);
                    break;
                case "auth/code-authenticate":
                    AuthCodeAuthenticate?.Invoke(this, args);
                    break;

                // CollectionsController
                case "api/collections/create":
                    CollectionsCreate?.Invoke(this, args);
                    break;
                case "api/collections/add-filters":
                    CollectionsAddFilters?.Invoke(this, args);
                    break;
                case "api/collections/remove-filters":
                    CollectionsRemoveFilters?.Invoke(this, args);
                    break;
                case "api/collections/delete":
                    CollectionsDelete?.Invoke(this, args);
                    break;
                case "api/collections/update":
                    CollectionsUpdate?.Invoke(this, args);
                    break;
                case "api/collections/get":
                    CollectionsGet?.Invoke(this, args);
                    break;
                case "api/collections/search":
                    CollectionsSearch?.Invoke(this, args);
                    break;
                case "api/collections/get-tracks":
                    CollectionsGetTracks?.Invoke(this, args);
                    break;

                // CreateDeleteController
                case "api/album/create":
                    CreateAlbum?.Invoke(this, args);
                    break;
                case "api/album/delete":
                    DeleteAlbum?.Invoke(this, args);
                    break;
                case "api/artist/create":
                    CreateArtist?.Invoke(this, args);
                    break;
                case "api/artist/delete":
                    DeleteArtist?.Invoke(this, args);
                    break;

                // DatabaseController
                case "api/db/format":
                    DbFormat?.Invoke(this, args);
                    break;
                case "api/db/bitrate":
                    DbBitrate?.Invoke(this, args);
                    break;
                case "api/dbsample-rate":
                    DbSampleRate?.Invoke(this, args);
                    break;
                case "api/db/bits-per-sample":
                    DbBitsPerSample?.Invoke(this, args);
                    break;
                case "api/db/channel":
                    DbChannel?.Invoke(this, args);
                    break;
                case "api/db/release-status":
                    DbReleaseStatus?.Invoke(this, args);
                    break;
                case "api/db/release-type":
                    DbReleaseType?.Invoke(this, args);
                    break;
                case "api/db/publisher":
                    DbPublisher?.Invoke(this, args);
                    break;
                case "api/db/genres":
                    DbGenres?.Invoke(this, args);
                    break;

                // DiscoverController
                case "api/discover/tracks":
                    DiscoverTracks?.Invoke(this, args);
                    break;
                case "api/discover/albums":
                    DiscoverAlbums?.Invoke(this, args);
                    break;
                case "api/discover/artists":
                    DiscoverArtists?.Invoke(this, args);
                    break;
                case "api/discover/time":
                    DiscoverTimeBasedTracks?.Invoke(this, args);
                    break;

                // DownloadController
                case "api/download/track":
                    DownloadTrack?.Invoke(this, args);
                    break;
                case "api/download/track-transcode":
                    DownloadTrackTranscode?.Invoke(this, args);
                    break;
                case "api/download/track-wave":
                    DownloadTrackWave?.Invoke(this, args);
                    break;
                case "api/download/track-art":
                    DownloadTrackArt?.Invoke(this, args);
                    break;
                case "api/download/album-art":
                    DownloadAlbumArt?.Invoke(this, args);
                    break;
                case "api/download/artist-pfp":
                    DownloadArtistPfp?.Invoke(this, args);
                    break;
                case "api/download/artist-banner":
                    DownloadArtistBanner?.Invoke(this, args);
                    break;
                case "api/download/playlist-art":
                    DownloadPlaylistArt?.Invoke(this, args);
                    break;
                case "api/download/collection-art":
                    DownloadCollectionArt?.Invoke(this, args);
                    break;

                // GeneralController
                case "api/track":
                    GetTrack?.Invoke(this, args);
                    break;
                case "api/tracks":
                    GetTracks?.Invoke(this, args);
                    break;
                case "api/album":
                    GetAlbum?.Invoke(this, args);
                    break;
                case "api/albums":
                    GetAlbums?.Invoke(this, args);
                    break;
                case "api/album/tracks":
                    GetAlbumTracks?.Invoke(this, args);
                    break;
                case "api/artist":
                    GetArtist?.Invoke(this, args);
                    break;
                case "api/artists":
                    GetArtists?.Invoke(this, args);
                    break;
                case "api/artist/tracks":
                    GetArtistTracks?.Invoke(this, args);
                    break;
                case "api/artist/releases":
                    GetArtistReleases?.Invoke(this, args);
                    break;
                case "api/artist/seen-on":
                    GetArtistSeenOn?.Invoke(this, args);
                    break;
                case "api/artist/connections":
                    GetArtistConnections?.Invoke(this, args);
                    break;
                case "api/lyrics":
                    GetLyrics?.Invoke(this, args);
                    break;

                // PlaylistsController
                case "api/playlists/create":
                    PlaylistsCreate?.Invoke(this, args);
                    break;
                case "api/playlists/add-tracks":
                    PlaylistsAddTracks?.Invoke(this, args);
                    break;
                case "api/playlists/remove-tracks":
                    PlaylistsRemoveTracks?.Invoke(this, args);
                    break;
                case "api/playlists/delete":
                    PlaylistsDelete?.Invoke(this, args);
                    break;
                case "api/playlists/update":
                    PlaylistsUpdate?.Invoke(this, args);
                    break;
                case "api/playlists/move-track":
                    PlaylistsMoveTrack?.Invoke(this, args);
                    break;
                case "api/playlists/get":
                    PlaylistsGet?.Invoke(this, args);
                    break;
                case "api/playlists/search":
                    PlaylistsSearch?.Invoke(this, args);
                    break;
                case "api/playlists/get-tracks":
                    PlaylistsGetTracks?.Invoke(this, args);
                    break;

                // QueuesController
                case "api/queues/create":
                    QueuesCreateFromTracks?.Invoke(this, args);
                    break;
                case "api/queues/create-from-albums":
                    QueuesCreateFromAlbums?.Invoke(this, args);
                    break;
                case "api/queues/create-from-artists":
                    QueuesCreateFromArtists?.Invoke(this, args);
                    break;
                case "api/queues/create-from-playlists":
                    QueuesCreateFromPlaylists?.Invoke(this, args);
                    break;
                case "api/queues/create-from-collections":
                    QueuesCreateFromCollections?.Invoke(this, args);
                    break;
                case "api/queues/add-tracks":
                    QueuesAddTracks?.Invoke(this, args);
                    break;
                case "api/queues/remove-tracks":
                    QueuesRemoveTracks?.Invoke(this, args);
                    break;
                case "api/queues/delete":
                    QueuesDelete?.Invoke(this, args);
                    break;
                case "api/queues/update-position":
                    QueuesUpdatePosition?.Invoke(this, args);
                    break;
                case "api/queues/update":
                    QueuesUpdate?.Invoke(this, args);
                    break;
                case "api/queues/move-track":
                    QueuesMoveTrack?.Invoke(this, args);
                    break;
                case "api/queues/get":
                    QueuesGet?.Invoke(this, args);
                    break;
                case "api/queues/search":
                    QueuesSearch?.Invoke(this, args);
                    break;
                case "api/queues/get-tracks":
                    QueuesGetTracks?.Invoke(this, args);
                    break;
                case "api/queues/shuffle":
                    QueuesShuffle?.Invoke(this, args);
                    break;

                // ScanController
                case "api/scan/start":
                    ScanStart?.Invoke(this, args);
                    break;
                case "api/scan/progress":
                    ScanProgress?.Invoke(this, args);
                    break;

                // SearchController
                case "api/search/tracks":
                    SearchTracks?.Invoke(this, args);
                    break;
                case "api/search/albums":
                    SearchAlbums?.Invoke(this, args);
                    break;
                case "api/search/artists":
                    SearchArtists?.Invoke(this, args);
                    break;

                // StatsController
                case "api/stats/log-play":
                    StatsLogPlay?.Invoke(this, args);
                    break;
                case "api/stats/log-skip":
                    StatsLogSkip?.Invoke(this, args);
                    break;
                case "api/stats/listening-time":
                    StatsListeningTime?.Invoke(this, args);
                    break;
                case "api/stats/top-tracks":
                    StatsTopTracks?.Invoke(this, args);
                    break;
                case "api/stats/top-ablums":
                    StatsTopAlbums?.Invoke(this, args);
                    break;
                case "api/stats/top-artists":
                    StatsTopArtists?.Invoke(this, args);
                    break;
                case "api/stats/top-genres":
                    StatsTopGenres?.Invoke(this, args);
                    break;
                case "api/stats/recent-tracks":
                    StatsRecentTrack?.Invoke(this, args);
                    break;
                case "api/stats/recent-albums":
                    StatsRecentAlbums?.Invoke(this, args);
                    break;
                case "api/stats/recent-artists":
                    StatsRecentArtists?.Invoke(this, args);
                    break;
                case "api/stats/rate-track":
                    StatsRateTrack?.Invoke(this, args);
                    break;
                case "api/stats/rate-album":
                    StatsRateAlbum?.Invoke(this, args);
                    break;
                case "api/stats/rate-artists":
                    StatsRateArtist?.Invoke(this, args);
                    break;

                // StreamController
                case "api/stream/connect":
                    StreamConnect?.Invoke(this, args);
                    break;
                case "api/stream/get-external":
                    StreamGetExternal?.Invoke(this, args);
                    break;
                case "api/stream/play-external":
                    StreamPlayExternal?.Invoke(this, args);
                    break;
                case "api/stream/pause-external":
                    StreamPauseExternal?.Invoke(this, args);
                    break;
                case "api/stream/skip-external":
                    StreamSkipExternal?.Invoke(this, args);
                    break;
                case "api/stream/rewind-external":
                    StreamRewindExternal?.Invoke(this, args);
                    break;
                case "api/stream/volume-external":
                    StreamVolumeExternal?.Invoke(this, args);
                    break;

                // UpdateController
                case "api/track/update":
                    UpdateTrack?.Invoke(this, args);
                    break;
                case "api/album/update":
                    UpdateAlbum?.Invoke(this, args);
                    break;
                case "api/artist/update":
                    UpdateArtist?.Invoke(this, args);
                    break;

                // UserController
                case "api/users/get":
                    UsersGet?.Invoke(this, args);
                    break;
                case "api/users/search":
                    UsersSearch?.Invoke(this, args);
                    break;
                case "api/users/add-friend":
                    UsersAddFriend?.Invoke(this, args);
                    break;
                case "api/users/remove-friend":
                    UsersRemoveFriend?.Invoke(this, args);
                    break;
                case "api/users/current":
                    UsersCurrent?.Invoke(this, args);
                    break;
                case "api/users/create":
                    UsersCreate?.Invoke(this, args);
                    break;
                case "api/users/delete":
                    UsersDelete?.Invoke(this, args);
                    break;
                case "api/users/update":
                    UsersUpdate?.Invoke(this, args);
                    break;
                case "api/users/change-username":
                    UsersChangeUsername?.Invoke(this, args);
                    break;
                case "api/users/change-password":
                    UsersChangePassword?.Invoke(this, args);
                    break;
            }
        }
    }
}
