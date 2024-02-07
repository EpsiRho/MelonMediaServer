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
    [Route("api/")]
    public class GeneralController : ControllerBase
    {
        private readonly ILogger<GeneralController> _logger;

        public GeneralController(ILogger<GeneralController> logger)
        {
            _logger = logger;
        }

        // Tracks
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("track")]
        public ObjectResult GetTrack(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);

            var trackDocs = TracksCollection.Find(trackFilter)
                                            .ToList();

            var track = trackDocs.FirstOrDefault();
            if(track == null)
            {
                return new ObjectResult("Track not found") { StatusCode = 404 };
            }

            var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
            usernames.Add(User.Identity.Name);

            if (track.PlayCounts != null)
            {
                track.PlayCounts = track.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
            }

            if (track.SkipCounts != null)
            {
                track.SkipCounts = track.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
            }

            if (track.Ratings != null)
            {
                track.Ratings = track.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
            }

            return new ObjectResult(track) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("track/update")]
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
            ShortAlbum newShortAlbum = null;
            List<Artist> newArtists = new List<Artist>();
            List<ShortArtist> newShortArtists = new List<ShortArtist>();
            if(albumId != "")
            {
                var aFilter = Builders<Album>.Filter.Eq(x=>x._id, albumId);
                newAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                if (newAlbum == null)
                {
                    return new ObjectResult("Album Not Found") { StatusCode = 404 };
                }
                newAlbum.Tracks.Add(new ShortTrack(foundTrack));
                var tFilter = Builders<Track>.Filter.In(a => a._id, newAlbum.Tracks.Select(x => x._id));
                var fullTracks = TracksCollection.Find(tFilter).ToList();
                newAlbum.Tracks = fullTracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).Select(x=>new ShortTrack() { _id = x._id, TrackName = x.TrackName}).ToList();
                newShortAlbum = new ShortAlbum(newAlbum);
                AlbumsCollection.ReplaceOne(aFilter, newAlbum);

                try
                {
                    var bFilter = Builders<Album>.Filter.Eq(x => x._id, foundTrack.Album._id);
                    var oldAlbum = AlbumsCollection.Find(aFilter).FirstOrDefault();
                    oldAlbum.Tracks.Remove(new ShortTrack(foundTrack));
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
                        newArtist.Tracks.Add(new ShortTrack(foundTrack));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }

                    newArtists.Add(newArtist);
                    newShortArtists.Add(new ShortArtist(newArtist));
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
                        newArtist.Tracks.Remove(new ShortTrack(foundTrack));
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

            var newShortTrack = new ShortTrack()
            {
                _id = foundTrack._id,
                TrackName = trackName
            };

            // Replace objects
            TracksCollection.ReplaceOne(trackFilter, newTrack);
            var albumFilter = Builders<Album>.Filter.ElemMatch(x => x.Tracks, Builders<ShortTrack>.Filter.Eq(x => x._id, foundTrack._id));
            var artistFilter = Builders<Artist>.Filter.ElemMatch(x => x.Tracks, Builders<ShortTrack>.Filter.Eq(x => x._id, foundTrack._id));
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

            return new ObjectResult("Track updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult GetTracks([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<Track> tracks = new List<Track>();
            foreach(var id in ids)
            {
                var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);
                var track = TracksCollection.Find(trackFilter).FirstOrDefault();
                if(track != null)
                {
                    var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
                    usernames.Add(User.Identity.Name);

                    if (track.PlayCounts != null)
                    {
                        track.PlayCounts = track.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (track.SkipCounts != null)
                    {
                        track.SkipCounts = track.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (track.Ratings != null)
                    {
                        track.Ratings = track.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    tracks.Add(track);
                }
            }


            return new ObjectResult(tracks) { StatusCode = 200 };
        }

        // Albums
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album")]
        public ObjectResult GetAlbum(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);

            var albumDocs = AlbumsCollection.Find(albumFilter)
                                            .ToList();

            var album = albumDocs.FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }
            album.Tracks = null;

            var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
            usernames.Add(User.Identity.Name);

            if (album.PlayCounts != null)
            {
                album.PlayCounts = album.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
            }

            if (album.SkipCounts != null)
            {
                album.SkipCounts = album.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
            }

            if (album.Ratings != null)
            {
                album.Ratings = album.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
            }

            return new ObjectResult(album) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("album/update")]
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
            List<ShortArtist> newAlbumShortArtists = new List<ShortArtist>();
            List<Artist> newConArtists = new List<Artist>();
            List<ShortArtist> newConShortArtists = new List<ShortArtist>();

            List<Track> newTracks = new List<Track>();
            List<ShortTrack> newShortTracks = new List<ShortTrack>();

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
                        newArtist.Releases.Add(new ShortAlbum(foundAlbum));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }

                    newAlbumArtists.Add(newArtist);
                    newAlbumShortArtists.Add(new ShortArtist(newArtist));
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
                        newArtist.Releases.Remove(new ShortAlbum(foundAlbum));
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
                        newArtist.SeenOn.Add(new ShortAlbum(foundAlbum));
                        ArtistsCollection.ReplaceOne(aFilter, newArtist);
                    }

                    newConArtists.Add(newArtist);
                    newConShortArtists.Add(new ShortArtist(newArtist));
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
                        newArtist.SeenOn.Remove(new ShortAlbum(foundAlbum));
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

            var newShortAlbum = new ShortAlbum()
            {
                _id = foundAlbum._id,
                AlbumName = albumName
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
                    newShortTracks.Add(new ShortTrack(newTrack));
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
            var artistFilter = Builders<Artist>.Filter.ElemMatch(x => x.Releases, Builders<ShortAlbum>.Filter.Eq(x => x._id, foundAlbum._id));
            var conArtistFilter = Builders<Artist>.Filter.ElemMatch(x => x.SeenOn, Builders<ShortAlbum>.Filter.Eq(x => x._id, foundAlbum._id));
            var trackUpdate = Builders<Track>.Update.Set(x=>x.Album, newShortAlbum);
            var artistUpdate = Builders<Artist>.Update.AddToSet("Releases.$", newShortAlbum);
            var conArtistUpdate = Builders<Artist>.Update.AddToSet("SeenOn.$", newShortAlbum);

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

            return new ObjectResult("Track updated") { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("album/tracks")]
        public ObjectResult GetAlbumTracks(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumsCollection = mongoDatabase.GetCollection<Album>("Albums");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);

            var albumDocs = AlbumsCollection.Find(albumFilter)
                                            .ToList();

            var album = albumDocs.FirstOrDefault();
            if (album == null)
            {
                return new ObjectResult("Album not found") { StatusCode = 404 };
            }
            List<Track> tracks = new List<Track>();
            for(uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Track>.Filter.Eq(x => x._id, album.Tracks[(int)i]._id);
                    var fullTrack = TracksCollection.Find(filter).FirstOrDefault();

                    var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
                    usernames.Add(User.Identity.Name);

                    if (fullTrack.PlayCounts != null)
                    {
                        fullTrack.PlayCounts = fullTrack.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullTrack.SkipCounts != null)
                    {
                        fullTrack.SkipCounts = fullTrack.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullTrack.Ratings != null)
                    {
                        fullTrack.Ratings = fullTrack.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    tracks.Add(fullTrack);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public ObjectResult GetAlbums([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<Album> albums = new List<Album>();
            foreach (var id in ids)
            {
                var albumFilter = Builders<Album>.Filter.Eq("AlbumId", id);
                var album = AlbumCollection.Find(albumFilter).FirstOrDefault();
                if (album != null)
                {
                    album.Tracks = null;
                    List<string> usernames =
                    [
                        User.Identity.Name,
                        .. UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username),
                    ];
                    try { album.PlayCounts = album.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList(); } catch (Exception) { }
                    try { album.SkipCounts = album.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList(); } catch (Exception) { }
                    try { album.Ratings = album.Ratings.Where(x => usernames.Contains(x.Username)).ToList(); } catch (Exception) { }
                    albums.Add(album);
                }
            }


            return new ObjectResult(albums) { StatusCode = 200 };
        }

        // Artists
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist")]
        public ObjectResult GetArtist(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", id);
            var ArtistDocs = ArtistCollection.Find(artistFilter)
                                            .ToList();

            var artist = ArtistDocs.FirstOrDefault();
            if(artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
            artist.Releases = null;
            artist.SeenOn = null;
            artist.Tracks = null;
            List<string> usernames =
            [
                User.Identity.Name,
                .. UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username),
            ];
            try { artist.PlayCounts = artist.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList(); } catch (Exception) { }
            try { artist.SkipCounts = artist.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList(); } catch (Exception) { }
            try { artist.Ratings = artist.Ratings.Where(x => usernames.Contains(x.Username)).ToList(); } catch (Exception) { }

            return new ObjectResult(artist) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/tracks")]
        public ObjectResult GetArtistTracks(string id, uint page = 0, uint count = 50)
        {
            if(page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            List<Track> tracks = new List<Track>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Track>.Filter.Eq(x => x._id, artist.Tracks[(int)i]._id);
                    var fullTrack = TracksCollection.Find(filter).FirstOrDefault();

                    var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
                    usernames.Add(User.Identity.Name);

                    if (fullTrack.PlayCounts != null)
                    {
                        fullTrack.PlayCounts = fullTrack.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullTrack.SkipCounts != null)
                    {
                        fullTrack.SkipCounts = fullTrack.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullTrack.Ratings != null)
                    {
                        fullTrack.Ratings = fullTrack.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    tracks.Add(fullTrack);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/releases")]
        public ObjectResult GetArtistReleases(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }

            List<Album> albums = new List<Album>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Album>.Filter.Eq(x => x._id, artist.Releases[(int)i]._id);
                    var fullAlbum = AlbumCollection.Find(filter).FirstOrDefault();
                    fullAlbum.Tracks = null;

                    var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
                    usernames.Add(User.Identity.Name);

                    if (fullAlbum.PlayCounts != null)
                    {
                        fullAlbum.PlayCounts = fullAlbum.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullAlbum.SkipCounts != null)
                    {
                        fullAlbum.SkipCounts = fullAlbum.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullAlbum.Ratings != null)
                    {
                        fullAlbum.Ratings = fullAlbum.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    albums.Add(fullAlbum);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artist/seen-on")]
        public ObjectResult GetArtistSeenOn(string id, uint page = 0, uint count = 50)
        {
            if (page > 100000 || count > 100000)
            {
                return new ObjectResult("Page / Count must be below 100000") { StatusCode = 400 };
            }
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistsCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            var artistFilter = Builders<Artist>.Filter.Eq(x=>x._id, id);

            var artistDocs = ArtistsCollection.Find(artistFilter)
                                            .ToList();

            var artist = artistDocs.FirstOrDefault();
            if (artist == null)
            {
                return new ObjectResult("Artist not found") { StatusCode = 404 };
            }
            List<Album> albums = new List<Album>();
            for (uint i = (page * count); i < ((page * count) + count); i++)
            {
                try
                {
                    var filter = Builders<Album>.Filter.Eq(x => x._id, artist.SeenOn[(int)i]._id);
                    var fullAlbum = AlbumCollection.Find(filter).FirstOrDefault();
                    fullAlbum.Tracks = null;

                    var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
                    usernames.Add(User.Identity.Name);

                    if (fullAlbum.PlayCounts != null)
                    {
                        fullAlbum.PlayCounts = fullAlbum.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullAlbum.SkipCounts != null)
                    {
                        fullAlbum.SkipCounts = fullAlbum.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (fullAlbum.Ratings != null)
                    {
                        fullAlbum.Ratings = fullAlbum.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    albums.Add(fullAlbum);
                }
                catch (Exception)
                {

                }
            }

            return new ObjectResult(albums) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public ObjectResult GetArtists([FromQuery] string[] ids)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");
            var UsersCollection = mongoDatabase.GetCollection<User>("Users");

            List<Artist> artists = new List<Artist>();
            foreach (var id in ids)
            {
                var artistFilter = Builders<Artist>.Filter.Eq("ArtistId", id);
                var artist = ArtistCollection.Find(artistFilter).FirstOrDefault();
                if (artist != null)
                {
                    artist.Releases = null;
                    artist.SeenOn = null;
                    artist.Tracks = null;

                    var usernames = new HashSet<string>(UsersCollection.Find(Builders<User>.Filter.Eq(x => x.PublicStats, true)).ToList().Select(x => x.Username));
                    usernames.Add(User.Identity.Name);

                    if (artist.PlayCounts != null)
                    {
                        artist.PlayCounts = artist.PlayCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (artist.SkipCounts != null)
                    {
                        artist.SkipCounts = artist.SkipCounts.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    if (artist.Ratings != null)
                    {
                        artist.Ratings = artist.Ratings.Where(x => usernames.Contains(x.Username)).ToList();
                    }

                    artists.Add(artist);
                }
            }


            return new ObjectResult(artists) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin")]
        [HttpPatch("artist/update")]
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

            List<ShortTrack> newTracks = new List<ShortTrack>();
            List<Track> missingTracks = new List<Track>();
            List<ShortAlbum> newReleases = new List<ShortAlbum>();
            List<Album> newFullReleases = new List<Album>();
            List<Album> missingReleases = new List<Album>();
            List<ShortAlbum> newSeenOns = new List<ShortAlbum>();


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
                        newAlbum.AlbumArtists.Add(new ShortArtist(foundArtist));
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                    }

                    newReleases.Add(new ShortAlbum(newAlbum));
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
                        newAlbum.AlbumArtists.Remove(new ShortArtist(foundArtist));
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
                        newAlbum.ContributingArtists.Add(new ShortArtist(foundArtist));
                        AlbumsCollection.ReplaceOne(aFilter, newAlbum);
                    }

                    newSeenOns.Add(new ShortAlbum(newAlbum));
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
                        newAlbum.ContributingArtists.Remove(new ShortArtist(foundArtist));
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
                        newTrack.TrackArtists.Add(new ShortArtist(foundArtist));
                        TracksCollection.ReplaceOne(tFilter, newTrack);
                    }

                    newTracks.Add(new ShortTrack(newTrack));
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

            var newShortArtist = new ShortArtist()
            {
                _id = foundArtist._id,
                ArtistName = artistName
            };

            // Replace objects
            ArtistsCollection.ReplaceOne(artistFilter, newArtist);

            var trackFilter = Builders<Track>.Filter.ElemMatch(x => x.TrackArtists, Builders<ShortArtist>.Filter.Eq(x => x._id, foundArtist._id));
            var albumArtistFilter = Builders<Album>.Filter.ElemMatch(x => x.AlbumArtists, Builders<ShortArtist>.Filter.Eq(x => x._id, foundArtist._id));
            var conArtistFilter = Builders<Album>.Filter.ElemMatch(x => x.ContributingArtists, Builders<ShortArtist>.Filter.Eq(x => x._id, foundArtist._id));
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

            return new ObjectResult("Artist updated") { StatusCode = 200 };
        }

        // Lyrics
        [Authorize(Roles = "Admin,User")]
        [HttpGet("lyrics")]
        public ObjectResult GetLyrics(string id)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");
            var trackFilter = Builders<Track>.Filter.Eq("TrackId", id);
            var track = TracksCollection.Find(trackFilter).FirstOrDefault();

            if(track == null)
            {
                return new ObjectResult("Track Not Found") { StatusCode = 404 };
            }

            if(track.LyricsPath == "")
            {
                return new ObjectResult("Track Does Not Have Lyrics") { StatusCode = 404 };
            }

            string txt = System.IO.File.ReadAllText(track.LyricsPath);

            return new ObjectResult(txt) { StatusCode = 200 };
        }
    }
}
