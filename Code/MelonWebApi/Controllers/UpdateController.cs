using Melon.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SharpCompress.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using Melon.LocalClasses;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using System.Web.Http.Filters;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Claims;
using System.Security.Policy;
using NuGet.Packaging.Signing;
using Amazon.Util.Internal;
using Melon.Classes;
using Melon.DisplayClasses;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/")]
    public class UpdateController : ControllerBase
    {
        private readonly ILogger<GeneralController> _logger;

        public UpdateController(ILogger<GeneralController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("track/update")]
        public ObjectResult UpdateTrack(string trackId, string disc = "", string isrc = "", string releaseDate = "", string position = "",
                                        [FromQuery] string[] trackGenres = null, string trackName = "", string year = "", string nextTrack = "",
                                        string albumId = "", int defaultTrackArt = -1, [FromQuery] string[] artistIds = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/track/update", curId, new Dictionary<string, object>()
            {
                { "trackId", trackId },
                { "disc", disc },
                { "isrc", isrc },
                { "releaseDate", releaseDate },
                { "position", position },
                { "trackGenres", trackGenres },
                { "trackName", trackName },
                { "year", year },
                { "nextTrack", nextTrack },
                { "albumId", albumId },
                { "defaultTrackArt", defaultTrackArt },
                { "artistIds", artistIds },
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            
            var trackFilter = Builders<Track>.Filter.Eq(x=>x._id, trackId);
            var foundTrack = TracksCollection.Find(trackFilter).FirstOrDefault();
            if(foundTrack == null)
            {
                args.SendEvent("Track Not Found", 404, Program.mWebApi);
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            disc = disc == "" ? foundTrack.Disc.ToString() : disc;
            isrc = isrc == "" ? foundTrack.ISRC : isrc;
            releaseDate = releaseDate == "" ? foundTrack.ReleaseDate.ToString() : releaseDate;
            position = position == "" ? foundTrack.Position.ToString() : position;
            trackName = trackName == "" ? foundTrack.Name.ToString() : trackName;
            year = year == "" ? foundTrack.Year : year;
            nextTrack = nextTrack == "" ? foundTrack.nextTrack : nextTrack;
            defaultTrackArt = defaultTrackArt == -1 ? foundTrack.TrackArtDefault : defaultTrackArt;

            if (trackGenres == null)
            {
                trackGenres = foundTrack.TrackGenres.ToArray();
            }

            Album newAlbum = null;
            DbLink newShortAlbum = null;
            List<Artist> newArtists = new List<Artist>();
            List<DbLink> newShortArtists = new List<DbLink>();
            if(albumId != "")
            {
                var aFilter = Builders<Album>.Filter.Eq(x=>x._id, albumId);
                newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();

                if (newAlbum == null)
                {
                    args.SendEvent("Album Not Found", 404, Program.mWebApi);
                    return new ObjectResult("Album Not Found") { StatusCode = 404 };
                }

                newShortAlbum = new DbLink(newAlbum);
            }
            else
            {
                newShortAlbum = foundTrack.Album;
            }

            if(artistIds != null)
            {
                foreach (var id in artistIds)
                {
                    var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                    var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                    if (newArtist == null)
                    {
                        args.SendEvent("Artist Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Artist Not Found") { StatusCode = 404 };
                    }
                    newShortArtists.Add(new DbLink(newArtist));

                    newArtists.Add(newArtist);
                    newShortArtists.Add(new DbLink(newArtist));
                }
            }
            else
            {
                newShortArtists.AddRange(foundTrack.TrackArtists);
            }

            // Create new objects
            var newTrack = new Track()
            {
                _id = foundTrack._id,
                Album = newShortAlbum,
                Bitrate = foundTrack.Bitrate,
                BitsPerSample = foundTrack.BitsPerSample,
                Channels = foundTrack.Channels,
                Disc = Convert.ToInt32(disc),
                Format = foundTrack.Format,
                ISRC = foundTrack.ISRC,
                MusicBrainzID = foundTrack.MusicBrainzID,
                Ratings = foundTrack.Ratings,
                ReleaseDate = DateTime.Parse(releaseDate).ToUniversalTime(),
                SampleRate = foundTrack.SampleRate,
                LyricsPath = foundTrack.LyricsPath,
                ServerURL = foundTrack.ServerURL,
                Position = Convert.ToInt32(position),
                TrackArtists = newShortArtists,
                TrackGenres = trackGenres.ToList(),
                Name = trackName,
                Year = year,
                Duration = foundTrack.Duration,
                DateAdded = foundTrack.DateAdded,
                LastModified = foundTrack.LastModified,
                Path = foundTrack.Path,
                PlayCounts = foundTrack.PlayCounts,
                SkipCounts = foundTrack.SkipCounts,
                TrackArtCount = foundTrack.TrackArtCount,
                nextTrack = nextTrack,
                TrackArtDefault = defaultTrackArt
            };

            var newShortTrack = new DbLink()
            {
                _id = foundTrack._id,
                Name = trackName
            };

            // Replace objects
            TracksCollection.ReplaceOne(trackFilter, newTrack);

            // Update File
            var fileMetadata = new ATL.Track(foundTrack.Path);

            fileMetadata.DiscNumber = disc == "" ? fileMetadata.DiscNumber : Convert.ToInt32(disc);
            if (!fileMetadata.AdditionalFields.ContainsKey("ISRC"))
            {
                fileMetadata.AdditionalFields.Add("ISRC", isrc);
            }
            fileMetadata.AdditionalFields["ISRC"] = isrc == "" ? fileMetadata.AdditionalFields["ISRC"] : isrc;
            fileMetadata.Date = releaseDate == "" ? fileMetadata.Date : DateTime.Parse(releaseDate).ToUniversalTime();
            fileMetadata.TrackNumber = position == "" ? fileMetadata.TrackNumber : Convert.ToInt32(position);
            fileMetadata.Title = trackName == "" ? fileMetadata.Title : trackName;
            fileMetadata.Year = year == "" ? fileMetadata.Year : Convert.ToInt32(year);

            if (trackGenres != null)
            {
                string genreStr = "";
                foreach (var genre in trackGenres)
                {
                    genreStr += $"{genre};";
                }
                genreStr = genreStr.Substring(0, genreStr.Length - 1);
                fileMetadata.Genre = genreStr;
            }

            if(newAlbum != null)
            {
                fileMetadata.Album = newAlbum.Name;
                fileMetadata.DiscTotal = newAlbum.TotalDiscs;
                fileMetadata.TrackTotal = newAlbum.TotalDiscs;
                fileMetadata.Publisher = newAlbum.Publisher;
                if (!fileMetadata.AdditionalFields.ContainsKey("RELEASESTATUS"))
                {
                    fileMetadata.AdditionalFields.Add("RELEASESTATUS", newAlbum.ReleaseStatus);
                }
                fileMetadata.AdditionalFields["RELEASESTATUS"] = newAlbum.ReleaseStatus;
                if (!fileMetadata.AdditionalFields.ContainsKey("RELEASETYPE"))
                {
                    fileMetadata.AdditionalFields.Add("RELEASETYPE", newAlbum.ReleaseType);
                }
                fileMetadata.AdditionalFields["RELEASETYPE"] = newAlbum.ReleaseType;
                string artistStr = "";
                foreach (var artist in newAlbum.AlbumArtists)
                {
                    artistStr = $"{artist};";
                }
                artistStr = artistStr.Substring(0, artistStr.Length - 1);
                fileMetadata.AlbumArtist = artistStr;
            }

            if(newArtists.Count() != 0)
            {
                string artistStr = "";
                foreach (var artist in newArtists)
                {
                    artistStr += $"{artist.Name};";
                }
                artistStr = artistStr.Substring(0, artistStr.Length - 1);
                fileMetadata.Artist = artistStr;
            }

            fileMetadata.Save();

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            args.SendEvent("Track updated", 200, Program.mWebApi);
            return new ObjectResult("Track updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("track/update/add-chapter")]
        public ObjectResult AddTrackChapter(string id, string title, long timestampMs, string description = "",
                                            [FromQuery] List<string> trackIds = null, [FromQuery] List<string> albumIds = null, [FromQuery] List<string> artistIds = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/track/update/add-chapter", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "title", title },
                { "timestampMs", timestampMs },
                { "description", description },
                { "trackIds", trackIds },
                { "albumIds", albumIds },
                { "artistIds", artistIds }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var trackFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var foundTrack = TracksCollection.Find(trackFilter).FirstOrDefault();
            if (foundTrack == null)
            {
                args.SendEvent("Track Not Found", 404, Program.mWebApi);
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            Chapter ch = new Chapter();
            ch._id = ObjectId.GenerateNewId().ToString();
            ch.Title = title;
            ch.Description = description;
            ch.Timestamp = TimeSpan.FromMilliseconds(timestampMs);
            ch.Tracks = trackIds != null ? TracksCollection.AsQueryable().Where(x=>trackIds.Contains(x._id)).ToList().Select(x=>new DbLink(x)).ToList() : new List<DbLink>();
            ch.Albums = albumIds != null ? AlbumsCollection.AsQueryable().Where(x => albumIds.Contains(x._id)).ToList().Select(x => new DbLink(x)).ToList() : new List<DbLink>();
            ch.Artists = artistIds != null ? ArtistsCollection.AsQueryable().Where(x => artistIds.Contains(x._id)).ToList().Select(x => new DbLink(x)).ToList() : new List<DbLink>();

            foundTrack.Chapters.Add(ch);
            TracksCollection.ReplaceOne(trackFilter, foundTrack);

            args.SendEvent("Track Chapter Added", 200, Program.mWebApi);
            return new ObjectResult("Track Chapter Added") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("track/update/remove-chapter")]
        public ObjectResult RemoveTrackChapter(string id, string chapterId)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                       .Where(c => c.Type == ClaimTypes.UserData)
                       .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/track/update/remove-chapter", curId, new Dictionary<string, object>()
            {
                { "id", id },
                { "chapterId", chapterId }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var trackFilter = Builders<Track>.Filter.Eq(x => x._id, id);
            var foundTrack = TracksCollection.Find(trackFilter).FirstOrDefault();
            if (foundTrack == null)
            {
                args.SendEvent("Track Not Found", 404, Program.mWebApi);
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            foundTrack.Chapters.Remove(foundTrack.Chapters.FirstOrDefault(x=>x._id == chapterId));
            TracksCollection.ReplaceOne(trackFilter, foundTrack);

            args.SendEvent("Track Chapter Removed", 200, Program.mWebApi);
            return new ObjectResult("Track Chapter Removed") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("album/update")]
        public ObjectResult UpdateAlbum(string albumId, string totalDiscs = "", string totalTracks = "", string releaseDate = "", string albumName = "",
                                        [FromQuery] string[] albumGenres = null, string bio = "", string publisher = "", string releaseStatus = "", string releaseType = "", int defaultAlbumArt = -1,
                                        [FromQuery] string[] contributingAristsIds = null, [FromQuery] string[] albumArtistIds = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/album/update", curId, new Dictionary<string, object>()
            {
                { "albumId", albumId },
                { "totalDiscs", totalDiscs },
                { "totalTracks", totalTracks },
                { "releaseDate", releaseDate },
                { "albumName", albumName },
                { "albumGenres", albumGenres },
                { "bio", bio },
                { "publisher", publisher },
                { "releaseStatus", releaseStatus },
                { "releaseType", releaseType },
                { "defaultAlbumArt", defaultAlbumArt },
                { "contributingAristsIds", contributingAristsIds },
                { "albumArtistIds", albumArtistIds },
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var albumFilter = Builders<Album>.Filter.Eq(x => x._id, albumId);
            var foundAlbum = AlbumsCollection.Find(albumFilter).FirstOrDefault();
            if (foundAlbum == null)
            {
                args.SendEvent("Album Not Found", 404, Program.mWebApi);
                return new ObjectResult("Album Not Found") { StatusCode = 404 };
            }

            List<Artist> newAlbumArtists = new List<Artist>();
            List<DbLink> newAlbumShortArtists = new List<DbLink>();
            List<Artist> newConArtists = new List<Artist>();
            List<DbLink> newConShortArtists = new List<DbLink>();

            List<Track> newTracks = new List<Track>();
            List<DbLink> newShortTracks = new List<DbLink>();

            if (albumArtistIds != null)
            {
                foreach (var id in albumArtistIds)
                {
                    var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                    var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                    if (newArtist == null)
                    {
                        args.SendEvent("Artist Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Artist Not Found") { StatusCode = 404 };
                    }

                    newAlbumArtists.Add(newArtist);
                    newAlbumShortArtists.Add(new DbLink(newArtist));
                }
            }
            else
            {
                newAlbumShortArtists.AddRange(foundAlbum.AlbumArtists);
            }

            if (contributingAristsIds != null)
            {
                foreach (var id in contributingAristsIds)
                {
                    var aFilter = Builders<Artist>.Filter.Eq(x => x._id, id);
                    var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                    if (newArtist == null)
                    {
                        args.SendEvent("Artist Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Artist Not Found") { StatusCode = 404 };
                    }

                    newConArtists.Add(newArtist);
                    newConShortArtists.Add(new DbLink(newArtist));
                }
            }
            else
            {
                newConShortArtists.AddRange(foundAlbum.ContributingArtists);
            }

            totalDiscs = totalDiscs == "" ? foundAlbum.TotalDiscs.ToString() : totalDiscs;
            totalTracks = totalTracks == "" ? foundAlbum.TotalTracks.ToString() : totalTracks;
            albumName = albumName == "" ? foundAlbum.Name : albumName;
            bio = bio == "" ? foundAlbum.Bio : bio;
            publisher = publisher == "" ? foundAlbum.Publisher : publisher;
            releaseStatus = releaseStatus == "" ? foundAlbum.ReleaseStatus : releaseStatus;
            releaseType = releaseType == "" ? foundAlbum.ReleaseType : releaseType;
            releaseDate = releaseDate == "" ? foundAlbum.ReleaseDate.ToString() : releaseDate;
            defaultAlbumArt = defaultAlbumArt == -1 ? foundAlbum.AlbumArtDefault : defaultAlbumArt;

            var newShortAlbum = new DbLink()
            {
                _id = foundAlbum._id,
                Name = albumName
            };

            if (albumGenres == null)
            {
                albumGenres = foundAlbum.AlbumGenres.ToArray();
            }

            // Create new objects
            var newAlbum = new Album()
            {
                _id = foundAlbum._id,
                AlbumGenres = albumGenres.ToList(),
                AlbumArtists = newAlbumShortArtists,
                AlbumArtPaths = foundAlbum.AlbumArtPaths,
                Name = albumName,
                ContributingArtists = newConShortArtists,
                PlayCounts = foundAlbum.PlayCounts,
                DateAdded = foundAlbum.DateAdded,
                SkipCounts = foundAlbum.SkipCounts,
                ReleaseDate = DateTime.Parse(releaseDate).ToUniversalTime(),
                ReleaseStatus = releaseStatus,
                ReleaseType = releaseType,
                Ratings = foundAlbum.Ratings,
                Bio = bio,
                Publisher = publisher,
                ServerURL = foundAlbum.ServerURL,
                TotalDiscs = Convert.ToInt32(totalDiscs),
                TotalTracks = Convert.ToInt32(totalTracks),
                AlbumArtDefault = defaultAlbumArt,
                AlbumArtCount = foundAlbum.AlbumArtCount
            };

            // Replace objects
            AlbumsCollection.ReplaceOne(albumFilter, newAlbum);

            var trackFilter = Builders<Track>.Filter.Eq(x => x.Album._id, foundAlbum._id);
            var trackUpdate = Builders<Track>.Update.Set(x=>x.Album, newShortAlbum);
            var artistUpdate = Builders<Artist>.Update.Set("Releases.$", newShortAlbum);
            var conArtistUpdate = Builders<Artist>.Update.Set("SeenOn.$", newShortAlbum);

            TracksCollection.UpdateMany(trackFilter, trackUpdate);

            // TODO: Update File

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            args.SendEvent("Album updated", 200, Program.mWebApi);
            return new ObjectResult("Album updated") { StatusCode = 200 };
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("artist/update")]
        public ObjectResult UpdateArtist(string artistId, string artistName = "", string bio = "", [FromQuery] string[] artistGenres = null, int defaultArtistPfp = -1, int defaultArtistBanner = -1)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist/update", curId, new Dictionary<string, object>()
            {
                { "artistId", artistId },
                { "artistName", artistName },
                { "bio", bio },
                { "artistGenres", artistGenres },
                { "defaultArtistPfp", defaultArtistPfp },
                { "defaultArtistBanner", defaultArtistBanner }
            });

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artistFilter = Builders<Artist>.Filter.Eq(x => x._id, artistId);
            var foundArtist = ArtistsCollection.Find(artistFilter).FirstOrDefault();
            if (foundArtist == null)
            {
                args.SendEvent("Artist Not Found", 404, Program.mWebApi);
                return new ObjectResult("Artist Not Found") { StatusCode = 404 };
            }

            if(foundArtist.ConnectedArtists == null)
            {
                foundArtist.ConnectedArtists = new List<DbLink>();
            }

            List<DbLink> newTracks = new List<DbLink>();
            List<Track> missingTracks = new List<Track>();
            List<DbLink> newReleases = new List<DbLink>();
            List<Album> newFullReleases = new List<Album>();
            List<Album> missingReleases = new List<Album>();
            List<DbLink> newSeenOns = new List<DbLink>();
            List<DbLink> ConnectedArtists = new List<DbLink>();

            artistName = artistName == "" ? foundArtist.Name : artistName;
            bio = bio == "" ? foundArtist.Bio : bio;
            defaultArtistPfp = defaultArtistPfp == -1 ? foundArtist.ArtistPfpDefault : defaultArtistPfp;
            defaultArtistBanner = defaultArtistBanner == -1 ? foundArtist.ArtistBannerArtDefault : defaultArtistBanner;

            if (artistGenres == null)
            {
                artistGenres = foundArtist.Genres.ToArray();
            }


            // Create new objects
            var newArtist = new Artist()
            {
                _id = foundArtist._id,
                Name = artistName,
                Bio = bio,
                PlayCounts = foundArtist.PlayCounts,
                Ratings = foundArtist.Ratings,
                SkipCounts = new List<UserStat>(),
                ServerURL = foundArtist.ServerURL,
                ArtistPfpPaths = foundArtist.ArtistPfpPaths,
                ArtistBannerPaths = foundArtist.ArtistBannerPaths,
                Genres = artistGenres.ToList(),
                DateAdded = foundArtist.DateAdded,
                ConnectedArtists = ConnectedArtists.ToList(),
                ArtistBannerArtCount = foundArtist.ArtistBannerArtCount,
                ArtistPfpArtCount = foundArtist.ArtistPfpArtCount,
                ArtistBannerArtDefault = defaultArtistBanner,
                ArtistPfpDefault = defaultArtistPfp
            };

            var newShortArtist = new DbLink()
            {
                _id = foundArtist._id,
                Name = artistName
            };

            // Replace objects
            ArtistsCollection.ReplaceOne(artistFilter, newArtist);

            var trackFilter = Builders<Track>.Filter.ElemMatch(x => x.TrackArtists, Builders<DbLink>.Filter.Eq(x => x._id, foundArtist._id));
            var albumArtistFilter = Builders<Album>.Filter.ElemMatch(x => x.AlbumArtists, Builders<DbLink>.Filter.Eq(x => x._id, foundArtist._id));
            var conArtistFilter = Builders<Album>.Filter.ElemMatch(x => x.ContributingArtists, Builders<DbLink>.Filter.Eq(x => x._id, foundArtist._id));
            var trackUpdate = Builders<Track>.Update.Set("TrackArtists.$", newShortArtist);
            var artistUpdate = Builders<Album>.Update.Set("AlbumArtists.$", newShortArtist);
            var conArtistUpdate = Builders<Album>.Update.Set("ContributingArtists.$", newShortArtist);

            TracksCollection.UpdateMany(trackFilter, trackUpdate);
            AlbumsCollection.UpdateMany(albumArtistFilter, artistUpdate);
            AlbumsCollection.UpdateMany(conArtistFilter, conArtistUpdate);

            // TODO: Update File
            //Task.Run(() =>
            //{
            //    
            //});

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            args.SendEvent("Artist updated", 200, Program.mWebApi);
            return new ObjectResult("Artist updated") { StatusCode = 200 };
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("update/check")]
        public ObjectResult CheckForUpdate()
        {
            var release = SettingsUI.GetGithubRelease("latest").Result;
            if (release != null)
            {
                var curVersion = System.Version.Parse(StateManager.Version);
                var latestVersion = System.Version.Parse(release.tag_name.Replace("v", ""));
                if (curVersion >= latestVersion)
                {
                    return new ObjectResult("No update found") { StatusCode = 204 };
                }

                UpdateResponse response = new UpdateResponse()
                {
                    CurrentVersion = curVersion.ToString(),
                    LatestVersion = latestVersion.ToString(),
                    ReleaseNotes = release.body
                };
                return new ObjectResult(response) { StatusCode = 200 };
            }
            return new ObjectResult("No update found") { StatusCode = 204 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("update/server")]
        public ObjectResult UpdateServer()
        {
            var release = SettingsUI.GetGithubRelease("latest").Result;
            if (release != null)
            {
                var curVersion = System.Version.Parse(StateManager.Version);
                var latestVersion = System.Version.Parse(release.tag_name.Replace("v", ""));
                if (curVersion >= latestVersion)
                {
                    return new ObjectResult("No update found") { StatusCode = 204 };
                }

                try
                {
                    var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MelonInstaller.exe");
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = $"-update -restart -installPath {AppDomain.CurrentDomain.BaseDirectory} -lang {StateManager.Language}",
                        UseShellExecute = false
                    };
                    Process.Start(processInfo);

                    Environment.Exit(0);
                }
                catch (Exception)
                {
                    return new ObjectResult("Updater failed to launch, is the updater missing?") { StatusCode = 500 };
                }
            }
            return new ObjectResult("No update found") { StatusCode = 204 };
        }

    }
}
