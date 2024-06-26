﻿using Melon.Models;
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
                newAlbum.Tracks.Add(new DbLink(foundTrack));
                var tFilter = Builders<Track>.Filter.In(a => a._id, newAlbum.Tracks.Select(x => x._id));
                var fullTracks = TracksCollection.Find(tFilter).ToList();
                newAlbum.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new DbLink() { _id = x._id, Name = x.Name }).ToList();
                newShortAlbum = new DbLink(newAlbum);
                AlbumsCollection.ReplaceOne(aFilter, newAlbum);

                try
                {
                    var bFilter = Builders<Album>.Filter.Eq(x => x._id, foundTrack.Album._id);
                    var oldAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                    oldAlbum.Tracks.Remove(new DbLink(foundTrack));
                    AlbumsCollection.ReplaceOne(bFilter, oldAlbum);
                }
                catch (Exception)
                {

                }
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

                    var items = (from artist in foundTrack.TrackArtists
                                where artist._id == id
                                select id).ToList();
                    if (items.Count() == 0) 
                    {
                        newArtist.Tracks.Add(new DbLink(foundTrack));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }

                    newArtists.Add(newArtist);
                    newShortArtists.Add(new DbLink(newArtist));
                }
                var missing = (from artist in foundTrack.TrackArtists
                              where !artistIds.Contains(artist._id)
                              select artist).ToList();
                foreach(var mArtist in missing)
                {
                    try
                    {
                        var aFilter = Builders<Artist>.Filter.Eq(x => x._id, mArtist._id);
                        var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                        newArtist.Tracks.Remove(new DbLink(foundTrack));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }
                    catch(Exception ex)
                    {

                    }
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
            var albumFilter = Builders<Album>.Filter.ElemMatch(x => x.Tracks, Builders<DbLink>.Filter.Eq(x => x._id, foundTrack._id));
            var artistFilter = Builders<Artist>.Filter.ElemMatch(x => x.Tracks, Builders<DbLink>.Filter.Eq(x => x._id, foundTrack._id));
            var albumnUpdate = Builders<Album>.Update.Set("Tracks.$", newShortTrack);
            var artistUpdate = Builders<Artist>.Update.Set("Tracks.$", newShortTrack);

            AlbumsCollection.UpdateMany(albumFilter, albumnUpdate);
            ArtistsCollection.UpdateMany(artistFilter, artistUpdate);

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
        public ObjectResult UpdateAlbum(string albumId, [FromQuery] string[] trackIds = null, string totalDiscs = "", string totalTracks = "", string releaseDate = "", string albumName = "",
                                        [FromQuery] string[] albumGenres = null, string bio = "", string publisher = "", string releaseStatus = "", string releaseType = "", int defaultAlbumArt = -1,
                                        [FromQuery] string[] contributingAristsIds = null, [FromQuery] string[] albumArtistIds = null)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/album/update", curId, new Dictionary<string, object>()
            {
                { "albumId", albumId },
                { "trackIds", trackIds },
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

                    var items = (from artist in foundAlbum.AlbumArtists
                                   where artist._id == id
                                   select artist).ToList();
                    if(items.Count() == 0)
                    {
                        newArtist.Releases.Add(new DbLink(foundAlbum));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }

                    newAlbumArtists.Add(newArtist);
                    newAlbumShortArtists.Add(new DbLink(newArtist));
                }
                var missing = (from artist in foundAlbum.AlbumArtists
                               where !albumArtistIds.Contains(artist._id)
                               select artist).ToList();
                foreach (var mArtist in missing)
                {
                    try
                    {
                        var aFilter = Builders<Artist>.Filter.Eq(x => x._id, mArtist._id);
                        var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                        newArtist.Releases.Remove(new DbLink(foundAlbum));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }
                    catch (Exception ex)
                    {

                    }
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

                    var items = (from artist in foundAlbum.ContributingArtists
                                 where artist._id == id
                                 select artist).ToList();
                    if (items.Count() == 0)
                    {
                        newArtist.SeenOn.Add(new DbLink(foundAlbum));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }

                    newConArtists.Add(newArtist);
                    newConShortArtists.Add(new DbLink(newArtist));
                }
                var missing = (from artist in foundAlbum.ContributingArtists
                               where !albumArtistIds.Contains(artist._id)
                               select artist).ToList();
                foreach (var mArtist in missing)
                {
                    try
                    {
                        var aFilter = Builders<Artist>.Filter.Eq(x => x._id, mArtist._id);
                        var newArtist = ArtistsCollection.Find(aFilter).FirstOrDefault();
                        newArtist.SeenOn.Remove(new DbLink(foundAlbum));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }
                    catch (Exception ex)
                    {

                    }
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

            if (trackIds != null)
            {
                foreach (var id in trackIds)
                {
                    var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
                    var newTrack = TracksCollection.Find(tFilter).FirstOrDefault();
                    if (newTrack == null)
                    {
                        args.SendEvent("Track Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Track Not Found") { StatusCode = 404 };
                    }

                    var items = (from track in foundAlbum.Tracks
                                 where track._id == id
                                 select track).ToList();
                    if (items.Count() == 0)
                    {
                        newTrack.Album = newShortAlbum;
                        TracksCollection.ReplaceOne(tFilter, newTrack);
                    }

                    newTracks.Add(newTrack);
                    newShortTracks.Add(new DbLink(newTrack));
                }
                var missing = (from track in foundAlbum.Tracks
                               where !trackIds.Contains(track._id)
                               select track).ToList();
                foreach (var mTrack in missing)
                {
                    try
                    {
                        var tFilter = Builders<Track>.Filter.Eq(x => x._id, mTrack._id);
                        var newTrack = TracksCollection.Find(tFilter).FirstOrDefault();
                        newTrack.Album = null;
                        TracksCollection.ReplaceOne(tFilter, newTrack);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                newShortTracks.AddRange(foundAlbum.Tracks);
            }

            

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
                Tracks = newShortTracks.ToList(),
                AlbumArtDefault = defaultAlbumArt,
                AlbumArtCount = foundAlbum.AlbumArtCount
            };

            

            // Replace objects
            AlbumsCollection.ReplaceOne(albumFilter, newAlbum);

            var trackFilter = Builders<Track>.Filter.Eq(x => x.Album._id, foundAlbum._id);
            var artistFilter = Builders<Artist>.Filter.ElemMatch(x => x.Releases, Builders<DbLink>.Filter.Eq(x => x._id, foundAlbum._id));
            var conArtistFilter = Builders<Artist>.Filter.ElemMatch(x => x.SeenOn, Builders<DbLink>.Filter.Eq(x => x._id, foundAlbum._id));
            var trackUpdate = Builders<Track>.Update.Set(x=>x.Album, newShortAlbum);
            var artistUpdate = Builders<Artist>.Update.Set("Releases.$", newShortAlbum);
            var conArtistUpdate = Builders<Artist>.Update.Set("SeenOn.$", newShortAlbum);

            TracksCollection.UpdateMany(trackFilter, trackUpdate);
            ArtistsCollection.UpdateMany(artistFilter, artistUpdate);
            ArtistsCollection.UpdateMany(conArtistFilter, conArtistUpdate);

            // Update File
            Task.Run(() =>
            {
                var ftFilter = Builders<Track>.Filter.In(a => a._id, newAlbum.Tracks.Select(x => x._id));
                var fullTracks = TracksCollection.Find(ftFilter).ToList();
                foreach (var track in fullTracks)
                {
                    var fileMetadata = new ATL.Track(track.Path);

                    fileMetadata.DiscTotal = totalDiscs == "" ? fileMetadata.DiscTotal : Convert.ToInt32(totalDiscs);
                    fileMetadata.TrackTotal = totalTracks == "" ? fileMetadata.TrackTotal : Convert.ToInt32(totalTracks);
                    fileMetadata.Album = albumName == "" ? fileMetadata.Album : albumName;
                    fileMetadata.Publisher = publisher == "" ? fileMetadata.Publisher : publisher;

                    if (!fileMetadata.AdditionalFields.ContainsKey("RELEASETYPE"))
                    {
                        fileMetadata.AdditionalFields.Add("RELEASETYPE", releaseType);
                    }
                    fileMetadata.AdditionalFields["RELEASETYPE"] = releaseType == "" ? fileMetadata.AdditionalFields["RELEASETYPE"] : releaseType;
                    if (!fileMetadata.AdditionalFields.ContainsKey("RELEASESTATUS"))
                    {
                        fileMetadata.AdditionalFields.Add("RELEASESTATUS", releaseType);
                    }
                    fileMetadata.AdditionalFields["RELEASESTATUS"] = releaseStatus == "" ? fileMetadata.AdditionalFields["RELEASESTATUS"] : releaseStatus;

                    if (newAlbumArtists.Count() != 0)
                    {
                        string artistStr = "";
                        foreach (var artist in newAlbumArtists)
                        {
                            artistStr += $"{artist.Name};";
                        }
                        artistStr = artistStr.Substring(0, artistStr.Length - 1);
                        fileMetadata.AlbumArtist = artistStr;
                    }

                    fileMetadata.Save();
                }
            });

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            args.SendEvent("Album updated", 200, Program.mWebApi);
            return new ObjectResult("Album updated") { StatusCode = 200 };
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("artist/update")]
        public ObjectResult UpdateArtist(string artistId, [FromQuery] string[] trackIds = null, [FromQuery] string[] releaseIds = null, [FromQuery] string[] seenOnIds = null,
                                         string artistName = "", string bio = "", [FromQuery] string[] artistGenres = null, int defaultArtistPfp = -1, int defaultArtistBanner = -1)
        {
            var curId = ((ClaimsIdentity)User.Identity).Claims
                        .Where(c => c.Type == ClaimTypes.UserData)
                        .Select(c => c.Value).FirstOrDefault();
            var args = new WebApiEventArgs("api/artist/update", curId, new Dictionary<string, object>()
            {
                { "artistId", artistId },
                { "trackIds", trackIds },
                { "releaseIds", releaseIds },
                { "seenOnIds", seenOnIds },
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


            if (releaseIds != null)
            {
                foreach (var id in releaseIds)
                {
                    var aFilter = Builders<Album>.Filter.Eq(x => x._id, id);
                    var newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                    if (newAlbum == null)
                    {
                        args.SendEvent("Album Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Album Not Found") { StatusCode = 404 };
                    }

                    if (foundArtist.Releases.Any(x => x._id == id))
                    {
                        newAlbum.AlbumArtists.Add(new DbLink(foundArtist));
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                    }

                    newReleases.Add(new DbLink(newAlbum));
                    foreach (var art in newAlbum.AlbumArtists)
                    {
                        if (!ConnectedArtists.Any(x => x._id == art._id))
                        {
                            ConnectedArtists.Add(art);
                        }
                    }
                    newFullReleases.Add(newAlbum);
                }
                var missing = (from album in foundArtist.Releases
                               where !trackIds.Contains(album._id)
                               select album).ToList();
                foreach (var mAlbum in missing)
                {
                    try
                    {
                        var aFilter = Builders<Album>.Filter.Eq(x => x._id, mAlbum._id);
                        var newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                        newAlbum.AlbumArtists.Remove(new DbLink(foundArtist));
                        foreach(var art in newAlbum.AlbumArtists)
                        {
                            ConnectedArtists.Remove(ConnectedArtists.Where(x=>x._id == art._id).FirstOrDefault());
                        }
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                        missingReleases.Add(newAlbum);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                newReleases.AddRange(foundArtist.Releases);
            }

            if (seenOnIds != null)
            {
                foreach (var id in seenOnIds)
                {
                    var aFilter = Builders<Album>.Filter.Eq(x => x._id, id);
                    var newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                    if (newAlbum == null)
                    {
                        args.SendEvent("Album Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Album Not Found") { StatusCode = 404 };
                    }

                    if (foundArtist.SeenOn.Any(x=>x._id == id))
                    {
                        newAlbum.ContributingArtists.Add(new DbLink(foundArtist));
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                    }

                    foreach (var art in newAlbum.ContributingArtists)
                    {
                        if (!ConnectedArtists.Any(x => x._id == art._id))
                        {
                            ConnectedArtists.Add(art);
                        }
                    }
                    newSeenOns.Add(new DbLink(newAlbum));
                }
                var missing = (from album in foundArtist.Releases
                               where !trackIds.Contains(album._id)
                               select album).ToList();
                foreach (var mAlbum in missing)
                {
                    try
                    {
                        var aFilter = Builders<Album>.Filter.Eq(x => x._id, mAlbum._id);
                        var newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                        newAlbum.ContributingArtists.Remove(new DbLink(foundArtist));
                        foreach (var art in newAlbum.ContributingArtists)
                        {
                            ConnectedArtists.Remove(ConnectedArtists.Where(x => x._id == art._id).FirstOrDefault());
                        }
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                newSeenOns.AddRange(foundArtist.SeenOn);
            }

            if (trackIds != null)
            {
                foreach (var id in trackIds)
                {
                    var tFilter = Builders<Track>.Filter.Eq(x => x._id, id);
                    var newTrack = TracksCollection.Find(tFilter).FirstOrDefault();
                    if (newTrack == null)
                    {
                        args.SendEvent("Track Not Found", 404, Program.mWebApi);
                        return new ObjectResult("Track Not Found") { StatusCode = 404 };
                    }

                    var items = (from track in foundArtist.Tracks
                                 where track._id == id
                                 select track).ToList();
                    if (items.Count() == 0)
                    {
                        newTrack.TrackArtists.Add(new DbLink(foundArtist));
                        TracksCollection.ReplaceOne(tFilter, newTrack);
                    }

                    foreach (var art in newTrack.TrackArtists)
                    {
                        if (!ConnectedArtists.Any(x => x._id == art._id))
                        {
                            ConnectedArtists.Add(art);
                        }
                    }
                    newTracks.Add(new DbLink(newTrack));
                }
            }
            else
            {
                newTracks.AddRange(foundArtist.Tracks);
            }

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
                Releases = newReleases,
                SeenOn = newSeenOns,
                Tracks = newTracks,
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

            // Update File
            Task.Run(() =>
            {
                if (foundArtist.Name != newArtist.Name)
                {
                    var ftFilter = Builders<Track>.Filter.In(a => a._id, newArtist.Tracks.Select(x => x._id));
                    var fullTracks = TracksCollection.Find(ftFilter).ToList();
                    foreach (var track in fullTracks)
                    {
                        var fileMetadata = new ATL.Track(track.Path);
                        string artistStr = fileMetadata.Artist;

                        if (!artistStr.Contains(newArtist.Name))
                        {
                            artistStr += $";{newArtist.Name}";
                        }
                        else
                        {
                            artistStr = artistStr.Replace(foundArtist.Name, newArtist.Name);
                        }
                        fileMetadata.Artist = artistStr;
                        fileMetadata.Save();

                    }

                    foreach (var item in missingTracks)
                    {
                        var fileMetadata = new ATL.Track(item.Path);
                        string artistStr = fileMetadata.Artist;

                        artistStr = artistStr.Replace(foundArtist.Name, "").Replace(";;", ";");
                        fileMetadata.Artist = artistStr;

                        fileMetadata.Save();
                    }
                }

                if (foundArtist.Releases != newArtist.Releases)
                {
                    foreach (var item in missingReleases)
                    {
                        var fFilter = Builders<Track>.Filter.In(a => a._id, item.Tracks.Select(x => x._id));
                        var fTracks = TracksCollection.Find(fFilter).ToList();
                        foreach (var track in fTracks)
                        {
                            var fileMetadata = new ATL.Track(track.Path);
                            string artistStr = fileMetadata.AlbumArtist;

                            artistStr = artistStr.Replace(foundArtist.Name, "").Replace(";;", ";");
                            fileMetadata.AlbumArtist = artistStr;

                            fileMetadata.Save();
                        }
                    }

                    foreach (var item in newFullReleases)
                    {
                        var fFilter = Builders<Track>.Filter.In(a => a._id, item.Tracks.Select(x => x._id));
                        var fTracks = TracksCollection.Find(fFilter).ToList();
                        foreach (var track in fTracks)
                        {
                            var fileMetadata = new ATL.Track(track.Path);
                            string artistStr = fileMetadata.AlbumArtist;

                            if (!artistStr.Contains(newArtist.Name))
                            {
                                artistStr += $";{newArtist.Name}";
                                fileMetadata.AlbumArtist = artistStr;
                                fileMetadata.Save();
                            }

                        }
                    }
                }
            });

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
