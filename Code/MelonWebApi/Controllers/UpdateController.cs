using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;
using System.Threading.Tasks;
using Melon.LocalClasses;
using System.Linq;
using System.Collections.Generic;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/update")]
    public class UpdateController : ControllerBase
    {
        private readonly ILogger<UpdateController> _logger;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Track> _tracksCollection;
        private readonly IMongoCollection<Album> _albumsCollection;
        private readonly IMongoCollection<Artist> _artistsCollection;

        public UpdateController(ILogger<UpdateController> logger)
        {
            _logger = logger;
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            _database = mongoClient.GetDatabase("Melon");
            _tracksCollection = _database.GetCollection<Track>("Tracks");
            _albumsCollection = _database.GetCollection<Album>("Albums");
            _artistsCollection = _database.GetCollection<Artist>("Artists");
        }

        /// <summary>
        /// Updates a track's metadata.
        /// </summary>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <param name="id">Track ID</param>
        /// <param name="updatedTrack">Updated track data</param>
        /// <returns>Result of the update operation</returns>
        /// <response code="200">If the update was successful</response>
        /// <response code="400">If the input is invalid</response>
        /// <response code="401">If the user is unauthorized</response>
        /// <response code="404">If the track is not found</response>
        [Authorize(Roles = "Admin")]
        [HttpPut("track")]
        public async Task<IActionResult> UpdateTrack(string id, [FromBody] ResponseTrack updatedTrack)
        {
            if (updatedTrack == null || string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid track data.");
            }

            var userId = User.FindFirstValue(ClaimTypes.UserData);
            var args = new WebApiEventArgs($"api/update/track/{id}", userId, new Dictionary<string, object>());

            var filter = Builders<Track>.Filter.Eq(t => t._id, id);
            var existingTrack = await _tracksCollection.Find(filter).FirstOrDefaultAsync();

            if (existingTrack == null)
            {
                args.SendEvent("Track not found", 404, Program.mWebApi);
                return NotFound("Track not found.");
            }

            // Preserve UserStats and ensure only the current user's stats can be modified
            updatedTrack.Ratings = UpdateUserStats(existingTrack.Ratings, updatedTrack.Ratings, userId);
            updatedTrack.PlayCounts = UpdateUserStats(existingTrack.PlayCounts, updatedTrack.PlayCounts, userId);
            updatedTrack.SkipCounts = UpdateUserStats(existingTrack.SkipCounts, updatedTrack.SkipCounts, userId);

            // Fetch full Album and Artist documents
            var album = await _albumsCollection.Find(a => a._id == updatedTrack.Album._id).FirstOrDefaultAsync();
            if (album == null)
            {
                return BadRequest("Invalid album reference.");
            }
            updatedTrack.Album = new DbLink(album);

            var artistIds = updatedTrack.TrackArtists.Select(a => a._id).ToList();
            var artists = await _artistsCollection.Find(a => artistIds.Contains(a._id)).ToListAsync();
            if (artists.Count != artistIds.Count)
            {
                return BadRequest("One or more track artists not found.");
            }
            updatedTrack.TrackArtists = artists.Select(a => new DbLink(a)).ToList();

            // Update the database record
            updatedTrack._id = existingTrack._id; // Ensure the ID remains the same
            var updatedTrackFull = new Track(updatedTrack);
            updatedTrackFull.Path = existingTrack.Path; // Preserve the file path
            updatedTrackFull.LyricsPath = existingTrack.LyricsPath; // Preserve the file path
            await _tracksCollection.ReplaceOneAsync(filter, updatedTrackFull);

            // Update the audio file's metadata
            try
            {
                var atlTrack = new ATL.Track(existingTrack.Path);

                // Update basic tags
                atlTrack.Title = updatedTrack.Name;
                atlTrack.Album = album.Name;
                atlTrack.Artist = string.Join("; ", updatedTrack.TrackArtists.Select(a => a.Name));
                atlTrack.TrackNumber = updatedTrack.Position;
                atlTrack.DiscNumber = updatedTrack.Disc;
                atlTrack.Year = int.TryParse(updatedTrack.Year, out int year) ? year : 0;
                atlTrack.AlbumArtist = string.Join("; ", album.AlbumArtists.Select(a => a.Name));
                atlTrack.Genre = string.Join("; ", updatedTrack.TrackGenres);
                atlTrack.Date = updatedTrack.ReleaseDate;

                // Update additional tags
                atlTrack.AdditionalFields["ISRC"] = updatedTrack.ISRC ?? "";
                atlTrack.AdditionalFields["MUSICBRAINZ_TRACKID"] = updatedTrack.MusicBrainzID ?? "";
                atlTrack.Publisher = album.Publisher ?? "";

                // Update chapters if supported by the format
                if (atlTrack.Chapters != null && updatedTrack.Chapters != null)
                {
                    atlTrack.Chapters.Clear();
                    foreach (var chapter in updatedTrack.Chapters)
                    {
                        var atlChapter = new ATL.ChapterInfo
                        {
                            Title = chapter.Title,
                            StartTime = (uint)chapter.Timestamp.TotalMilliseconds,
                            Subtitle = chapter.Description
                        };
                        atlTrack.Chapters.Add(atlChapter);
                    }
                }

                atlTrack.Save();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file metadata for track ID: {TrackId}", id);
                args.SendEvent("Error updating file metadata", 500, Program.mWebApi);
                return StatusCode(500, "Error updating file metadata.");
            }

            args.SendEvent("Track updated successfully", 200, Program.mWebApi);
            return Ok("Track updated successfully.");
        }

        /// <summary>
        /// Updates an album's metadata.
        /// </summary>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <param name="id">Album ID</param>
        /// <param name="updatedAlbum">Updated album data</param>
        /// <returns>Result of the update operation</returns>
        /// <response code="200">If the update was successful</response>
        /// <response code="400">If the input is invalid</response>
        /// <response code="401">If the user is unauthorized</response>
        /// <response code="404">If the album is not found</response>
        [Authorize(Roles = "Admin")]
        [HttpPut("album")]
        public async Task<IActionResult> UpdateAlbum(string id, [FromBody] ResponseAlbum updatedAlbum)
        {
            if (updatedAlbum == null || string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid album data.");
            }

            var userId = User.FindFirstValue(ClaimTypes.UserData);
            var args = new WebApiEventArgs($"api/update/album/{id}", userId, new Dictionary<string, object>());

            var filter = Builders<Album>.Filter.Eq(a => a._id, id);
            var existingAlbum = await _albumsCollection.Find(filter).FirstOrDefaultAsync();

            if (existingAlbum == null)
            {
                args.SendEvent("Album not found", 404, Program.mWebApi);
                return NotFound("Album not found.");
            }

            // Preserve UserStats and ensure only the current user's stats can be modified
            updatedAlbum.Ratings = UpdateUserStats(existingAlbum.Ratings, updatedAlbum.Ratings, userId);
            updatedAlbum.PlayCounts = UpdateUserStats(existingAlbum.PlayCounts, updatedAlbum.PlayCounts, userId);
            updatedAlbum.SkipCounts = UpdateUserStats(existingAlbum.SkipCounts, updatedAlbum.SkipCounts, userId);

            // Fetch full Artist documents for AlbumArtists and ContributingArtists
            var albumArtistIds = updatedAlbum.AlbumArtists.Select(a => a._id).ToList();
            var albumArtists = await _artistsCollection.Find(a => albumArtistIds.Contains(a._id)).ToListAsync();
            if (albumArtists.Count != albumArtistIds.Count)
            {
                return BadRequest("One or more album artists not found.");
            }
            updatedAlbum.AlbumArtists = albumArtists.Select(a => new DbLink(a)).ToList();

            var contribArtistIds = updatedAlbum.ContributingArtists.Select(a => a._id).ToList();
            var contribArtists = await _artistsCollection.Find(a => contribArtistIds.Contains(a._id)).ToListAsync();
            updatedAlbum.ContributingArtists = contribArtists.Select(a => new DbLink(a)).ToList();

            // Update the database record
            updatedAlbum._id = existingAlbum._id; // Ensure the ID remains the same
            var updatedAlbumFull = new Album(updatedAlbum);
            updatedAlbumFull.AlbumArtPaths = existingAlbum.AlbumArtPaths; // Preserve the file path
            await _albumsCollection.ReplaceOneAsync(filter, updatedAlbumFull);

            // Find all tracks in this album
            var trackFilter = Builders<Track>.Filter.Eq(t => t.Album._id, id);
            var albumTracks = await _tracksCollection.Find(trackFilter).ToListAsync();

            // Update the album information in each track and the corresponding file metadata
            foreach (var track in albumTracks)
            {
                track.Album = new DbLink(updatedAlbumFull);
                await _tracksCollection.ReplaceOneAsync(Builders<Track>.Filter.Eq(t => t._id, track._id), track);

                // Update the audio file's metadata
                try
                {
                    var atlTrack = new ATL.Track(track.Path);
                    atlTrack.Album = updatedAlbum.Name;
                    atlTrack.AlbumArtist = string.Join("; ", updatedAlbum.AlbumArtists.Select(a => a.Name));
                    atlTrack.Publisher = updatedAlbum.Publisher ?? "";
                    atlTrack.Year = updatedAlbum.ReleaseDate.Year;
                    atlTrack.AdditionalFields["RELEASESTATUS"] = updatedAlbum.ReleaseStatus ?? "";
                    atlTrack.AdditionalFields["RELEASETYPE"] = updatedAlbum.ReleaseType ?? "";
                    atlTrack.Save();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating file metadata for track ID: {TrackId}", track._id);
                    // Continue to next track without failing the entire operation
                }
            }

            args.SendEvent("Album updated successfully", 200, Program.mWebApi);
            return Ok("Album updated successfully.");
        }

        /// <summary>
        /// Updates an artist's metadata.
        /// </summary>
        /// <remarks>
        /// ### Authorization: JWT
        /// - **Valid roles**: Admin
        /// </remarks>
        /// <param name="id">Artist ID</param>
        /// <param name="updatedArtist">Updated artist data</param>
        /// <returns>Result of the update operation</returns>
        /// <response code="200">If the update was successful</response>
        /// <response code="400">If the input is invalid</response>
        /// <response code="401">If the user is unauthorized</response>
        /// <response code="404">If the artist is not found</response>
        [Authorize(Roles = "Admin")]
        [HttpPut("artist")]
        public async Task<IActionResult> UpdateArtist(string id, [FromBody] Artist updatedArtist)
        {
            if (updatedArtist == null || string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid artist data.");
            }

            var userId = User.FindFirstValue(ClaimTypes.UserData);
            var args = new WebApiEventArgs($"api/update/artist/{id}", userId, new Dictionary<string, object>());

            var filter = Builders<Artist>.Filter.Eq(a => a._id, id);
            var existingArtist = await _artistsCollection.Find(filter).FirstOrDefaultAsync();

            if (existingArtist == null)
            {
                args.SendEvent("Artist not found", 404, Program.mWebApi);
                return NotFound("Artist not found.");
            }

            // Preserve UserStats and ensure only the current user's stats can be modified
            updatedArtist.Ratings = UpdateUserStats(existingArtist.Ratings, updatedArtist.Ratings, userId);
            updatedArtist.PlayCounts = UpdateUserStats(existingArtist.PlayCounts, updatedArtist.PlayCounts, userId);
            updatedArtist.SkipCounts = UpdateUserStats(existingArtist.SkipCounts, updatedArtist.SkipCounts, userId);

            // Update the database record
            updatedArtist._id = existingArtist._id; // Ensure the ID remains the same
            await _artistsCollection.ReplaceOneAsync(filter, updatedArtist);

            // Find all tracks where the artist is credited
            var trackFilter = Builders<Track>.Filter.ElemMatch(t => t.TrackArtists, a => a._id == id);
            var artistTracks = await _tracksCollection.Find(trackFilter).ToListAsync();

            // Update the artist information in each track and the corresponding file metadata
            foreach (var track in artistTracks)
            {
                // Update artist info in track
                foreach (var artist in track.TrackArtists)
                {
                    if (artist._id == id)
                    {
                        artist.Name = updatedArtist.Name;
                    }
                }

                await _tracksCollection.ReplaceOneAsync(Builders<Track>.Filter.Eq(t => t._id, track._id), track);

                // Update the audio file's metadata
                try
                {
                    var atlTrack = new ATL.Track(track.Path);
                    atlTrack.Artist = string.Join("; ", track.TrackArtists.Select(a => a.Name));
                    atlTrack.Save();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating file metadata for track ID: {TrackId}", track._id);
                    // Continue to next track without failing the entire operation
                }
            }

            // Update the artist's id in albums where they are credited as album artist
            var albumFilter = Builders<Album>.Filter.ElemMatch(a => a.AlbumArtists, ar => ar._id == id);
            var artistAlbums = await _albumsCollection.Find(albumFilter).ToListAsync();

            foreach (var album in artistAlbums)
            {
                foreach (var artist in album.AlbumArtists)
                {
                    if (artist._id == id)
                    {
                        artist.Name = updatedArtist.Name;
                    }
                }

                await _albumsCollection.ReplaceOneAsync(Builders<Album>.Filter.Eq(a => a._id, album._id), album);

                // Update the album info in tracks
                var albumTracksFilter = Builders<Track>.Filter.Eq(t => t.Album._id, album._id);
                var albumTracks = await _tracksCollection.Find(albumTracksFilter).ToListAsync();

                foreach (var track in albumTracks)
                {
                    track.Album = new DbLink(album);
                    await _tracksCollection.ReplaceOneAsync(Builders<Track>.Filter.Eq(t => t._id, track._id), track);

                    // Update the audio file's metadata
                    try
                    {
                        var atlTrack = new ATL.Track(track.Path);
                        atlTrack.AlbumArtist = string.Join("; ", album.AlbumArtists.Select(a => a.Name));
                        atlTrack.Save();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating file metadata for track ID: {TrackId}", track._id);
                        // Continue to next track
                    }
                }
            }

            // Update the artist's id in albums where they are credited as album artist
            var albumContributingFilter = Builders<Album>.Filter.ElemMatch(a => a.ContributingArtists, ar => ar._id == id);
            var artistContributingAlbums = await _albumsCollection.Find(albumFilter).ToListAsync();

            foreach (var album in artistContributingAlbums)
            {
                foreach (var artist in album.ContributingArtists)
                {
                    if (artist._id == id)
                    {
                        artist.Name = updatedArtist.Name;
                    }
                }

                await _albumsCollection.ReplaceOneAsync(Builders<Album>.Filter.Eq(a => a._id, album._id), album);

            }

            args.SendEvent("Artist updated successfully", 200, Program.mWebApi);
            return Ok("Artist updated successfully.");
        }

        /// <summary>
        /// Updates the UserStats for the current user.
        /// </summary>
        /// <param name="existingStats">Existing stats from the database</param>
        /// <param name="updatedStats">Stats provided in the update request</param>
        /// <param name="userId">Current user's ID</param>
        /// <returns>Updated list of UserStats</returns>
        private List<UserStat> UpdateUserStats(List<UserStat> existingStats, List<UserStat> updatedStats, string userId)
        {
            if (existingStats == null) existingStats = new List<UserStat>();
            if (updatedStats == null) updatedStats = new List<UserStat>();

            // Get the current user's stats
            var userStat = existingStats.FirstOrDefault(s => s.UserId == userId);

            var updatedUserStat = updatedStats.FirstOrDefault(s => s.UserId == userId);

            if (updatedUserStat != null)
            {
                if (userStat != null)
                {
                    // Update existing stat
                    userStat.Value = updatedUserStat.Value;
                }
                else
                {
                    // Add new stat
                    existingStats.Add(new UserStat
                    {
                        UserId = userId,
                        Value = updatedUserStat.Value
                    });
                }
            }

            return existingStats;
        }
    }
}
