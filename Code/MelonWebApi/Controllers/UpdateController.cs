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

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/update")]
    public class UpdateController : ControllerBase
    {
        private readonly ILogger<GeneralController> _logger;

        public UpdateController(ILogger<GeneralController> logger)
        {
            _logger = logger;
        }

        // Tracks
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("track")]
        public ObjectResult UpdateTrack(string trackId, string disc = "", string isrc = "", string releaseDate = "", string position = "",
                                        [FromQuery] string[] trackGenres = null, string trackName = "", string year = "", string nextTrack = "",
                                        string albumId = "", [FromQuery] string[] artistIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            
            var trackFilter = Builders<Track>.Filter.Eq(x=>x._id, trackId);
            var foundTrack = TracksCollection.Find(trackFilter).FirstOrDefault();
            if(foundTrack == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            disc = disc == "" ? foundTrack.Disc.ToString() : disc;
            isrc = isrc == "" ? foundTrack.ISRC : isrc;
            releaseDate = releaseDate == "" ? foundTrack.ReleaseDate.ToString() : releaseDate;
            position = position == "" ? foundTrack.Position.ToString() : position;
            trackName = trackName == "" ? foundTrack.TrackName.ToString() : trackName;
            year = year == "" ? foundTrack.Year : year;
            nextTrack = nextTrack == "" ? foundTrack.nextTrack : nextTrack;

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
                    return new ObjectResult("Album Not Found") { StatusCode = 404 };
                }
                newAlbum.Tracks.Add(new DbLink(foundTrack));
                var tFilter = Builders<Track>.Filter.In(a => a._id, newAlbum.Tracks.Select(x => x._id));
                var fullTracks = TracksCollection.Find(tFilter).ToList();
                newAlbum.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new DbLink() { _id = x._id, Name = x.TrackName}).ToList();
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
                TrackName = trackName,
                Year = year,
                Duration = foundTrack.Duration,
                DateAdded = foundTrack.DateAdded,
                LastModified = foundTrack.LastModified,
                Path = foundTrack.Path,
                PlayCounts = foundTrack.PlayCounts,
                SkipCounts = foundTrack.SkipCounts,
                TrackArtCount = foundTrack.TrackArtCount,
                nextTrack = nextTrack,
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
                fileMetadata.Album = newAlbum.AlbumName;
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
                    artistStr += $"{artist.ArtistName};";
                }
                artistStr = artistStr.Substring(0, artistStr.Length - 1);
                fileMetadata.Artist = artistStr;
            }

            fileMetadata.Save();

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            return new ObjectResult("Track updated") { StatusCode = 200 };
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("album")]
        public ObjectResult UpdateAlbum(string albumId, [FromQuery] string[] trackIds = null, string totalDiscs = "", string totalTracks = "", string releaseDate = "", string albumName = "",
                                        [FromQuery] string[] albumGenres = null, string bio = "", string publisher = "", string releaseStatus = "", string releaseType = "",
                                        [FromQuery] string[] contributingAristsIds = null, [FromQuery] string[] albumArtistIds = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var albumFilter = Builders<Album>.Filter.Eq(x => x._id, albumId);
            var foundAlbum = AlbumsCollection.Find(albumFilter).FirstOrDefault();
            if (foundAlbum == null)
            {
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
            albumName = albumName == "" ? foundAlbum.AlbumName : albumName;
            bio = bio == "" ? foundAlbum.Bio : bio;
            publisher = publisher == "" ? foundAlbum.Publisher : publisher;
            releaseStatus = releaseStatus == "" ? foundAlbum.ReleaseStatus : releaseStatus;
            releaseType = releaseType == "" ? foundAlbum.ReleaseType : releaseType;
            releaseDate = releaseDate == "" ? foundAlbum.ReleaseDate.ToString() : releaseDate;

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
                AlbumName = albumName,
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
                Tracks = newShortTracks.ToList()
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
                        artistStr += $"{artist.ArtistName};";
                    }
                    artistStr = artistStr.Substring(0, artistStr.Length - 1);
                    fileMetadata.AlbumArtist = artistStr;
                }

                fileMetadata.Save();
            }

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            return new ObjectResult("Track updated") { StatusCode = 200 };
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("artist")]
        public ObjectResult UpdateArtist(string artistId, [FromQuery] string[] trackIds = null, [FromQuery] string[] releaseIds = null, [FromQuery] string[] seenOnIds = null,
                                         string artistName = "", string bio = "", [FromQuery] string[] artistGenres = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var artistFilter = Builders<Artist>.Filter.Eq(x => x._id, artistId);
            var foundArtist = ArtistsCollection.Find(artistFilter).FirstOrDefault();
            if (foundArtist == null)
            {
                return new ObjectResult("Artist Not Found") { StatusCode = 404 };
            }

            List<DbLink> newTracks = new List<DbLink>();
            List<Track> missingTracks = new List<Track>();
            List<DbLink> newReleases = new List<DbLink>();
            List<Album> newFullReleases = new List<Album>();
            List<Album> missingReleases = new List<Album>();
            List<DbLink> newSeenOns = new List<DbLink>();


            if (releaseIds != null)
            {
                foreach (var id in releaseIds)
                {
                    var aFilter = Builders<Album>.Filter.Eq(x => x._id, id);
                    var newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                    if (newAlbum == null)
                    {
                        return new ObjectResult("Album Not Found") { StatusCode = 404 };
                    }

                    var items = (from album in foundArtist.Releases
                                 where album._id == id
                                 select album).ToList();
                    if (items.Count() == 0)
                    {
                        newAlbum.AlbumArtists.Add(new DbLink(foundArtist));
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                    }

                    newReleases.Add(new DbLink(newAlbum));
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
                        return new ObjectResult("Album Not Found") { StatusCode = 404 };
                    }

                    var items = (from album in foundArtist.SeenOn
                                 where album._id == id
                                 select album).ToList();
                    if (items.Count() == 0)
                    {
                        newAlbum.ContributingArtists.Add(new DbLink(foundArtist));
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
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

                    newTracks.Add(new DbLink(newTrack));
                }
            }
            else
            {
                newTracks.AddRange(foundArtist.Tracks);
            }

            artistName = artistName == "" ? foundArtist.ArtistName : artistName;
            bio = bio == "" ? foundArtist.Bio : bio;

            if (artistGenres == null)
            {
                artistGenres = foundArtist.Genres.ToArray();
            }


            // Create new objects
            var newArtist = new Artist()
            {
                _id = foundArtist._id,
                ArtistName = artistName,
                Bio = bio,
                PlayCounts = foundArtist.PlayCounts,
                Ratings = foundArtist.Ratings,
                SkipCounts = new List<UserStat>(),
                ServerURL = foundArtist.ServerURL,
                ArtistArtPaths = foundArtist.ArtistArtPaths,
                ArtistBannerPaths = foundArtist.ArtistBannerPaths,
                Genres = artistGenres.ToList(),
                DateAdded = foundArtist.DateAdded,
                Releases = newReleases,
                SeenOn = newSeenOns,
                Tracks = newTracks,
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
            var ftFilter = Builders<Track>.Filter.In(a => a._id, newArtist.Tracks.Select(x => x._id));
            var fullTracks = TracksCollection.Find(ftFilter).ToList();
            foreach (var track in fullTracks)
            {
                var fileMetadata = new ATL.Track(track.Path);
                string artistStr = fileMetadata.Artist;

                if (!artistStr.Contains(newArtist.ArtistName))
                {
                    artistStr += $";{newArtist.ArtistName}";
                    fileMetadata.Artist = artistStr;
                    fileMetadata.Save();
                }
                
            }

            foreach (var item in missingTracks)
            {
                var fileMetadata = new ATL.Track(item.Path);
                string artistStr = fileMetadata.Artist;

                artistStr = artistStr.Replace(foundArtist.ArtistName, "").Replace(";;", ";");
                fileMetadata.Artist = artistStr;

                fileMetadata.Save();
            }

            foreach (var item in missingReleases)
            {
                var fFilter = Builders<Track>.Filter.In(a => a._id, item.Tracks.Select(x => x._id));
                var fTracks = TracksCollection.Find(fFilter).ToList();
                foreach (var track in fTracks)
                {
                    var fileMetadata = new ATL.Track(track.Path);
                    string artistStr = fileMetadata.AlbumArtist;

                    artistStr = artistStr.Replace(foundArtist.ArtistName, "").Replace(";;", ";");
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

                    if (!artistStr.Contains(newArtist.ArtistName))
                    {
                        artistStr += $";{newArtist.ArtistName}";
                        fileMetadata.AlbumArtist = artistStr;
                        fileMetadata.Save();
                    }

                }
            }

            Thread t = new Thread(MelonScanner.UpdateCollections);
            t.Start();

            return new ObjectResult("Artist updated") { StatusCode = 200 };
        }

    }
}
