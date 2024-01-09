using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Data;
using MongoDB.Driver;
using Melon.LocalClasses;
using System.Diagnostics;
using Melon.Models;
using RestSharp;
using ATL.Logging;
using System.Text.Json;
using Azure.Core;
using Newtonsoft.Json;

namespace MelonWebApi.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;

        public SearchController(ILogger<SearchController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("tracks")]
        public ObjectResult SearchTracks(int page = 1, int count = 50, string trackName = "", string format = "", string bitrate = "", 
                                               string sampleRate = "", string channels = "", string bitsPerSample = "", string year = "", 
                                               long ltPlayCount = 0, long gtPlayCount = 0, long ltSkipCount = 0, long gtSkipCount = 0, int ltYear = 0, int ltMonth = 0, int ltDay = 0,
                                               int gtYear = 0, int gtMonth = 0, int gtDay = 0, long ltRating = 0, long gtRating = 0, [FromQuery] string[] genres = null, bool externalResults = false)
        {
            List<Track> tracks = new List<Track>();
            int shortCount = count;
            if (externalResults)
            {
                shortCount = count / (Security.Connections.Count() + 1);
                foreach (var con in Security.Connections)
                {
                    try
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            // Set the base URI for HTTP requests
                            client.BaseAddress = new Uri(con.URL);


                            try
                            {
                                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", con.JWT);
                                HttpResponseMessage checkResponse = client.GetAsync($"/auth/check").Result;

                                if (checkResponse.StatusCode != System.Net.HttpStatusCode.OK)
                                {
                                    HttpResponseMessage authResponse = client.GetAsync($"/auth/login?username={con.Username}&password={con.Password}").Result;
                                    con.JWT = authResponse.Content.ReadAsStringAsync().Result;
                                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", con.JWT);
                                }

                                // Get JWT token
                                string sURL = $"/api/search/tracks?page={page}";
                                sURL += $"&count={shortCount}";
                                sURL += $"&trackName={trackName}";
                                sURL += $"&format={format}";
                                sURL += $"&bitrate={bitrate}";
                                sURL += $"&sampleRate={sampleRate}";
                                sURL += $"&bitsPerSample={bitsPerSample}";
                                sURL += $"&year={year}";
                                sURL += $"&ltPlayCount={ltPlayCount}";
                                sURL += $"&gtPlayCount={gtPlayCount}";
                                sURL += $"&ltSkipCount={ltSkipCount}";
                                sURL += $"&gtSkipCount={gtSkipCount}";
                                sURL += $"&ltYear={ltYear}";
                                sURL += $"&gtYear={gtYear}";
                                sURL += $"&ltMonth={ltMonth}";
                                sURL += $"&gtMonth={gtMonth}";
                                sURL += $"&ltDay={ltDay}";
                                sURL += $"&gtDay={gtDay}";
                                sURL += $"&ltRating={ltRating}";
                                sURL += $"&gtRating={gtRating}";
                                if (genres != null)
                                {
                                    foreach (var genre in genres)
                                    {
                                        sURL += $"&genres={genre}";

                                    }
                                }
                                HttpResponseMessage response = client.GetAsync(sURL).Result;

                                var tempTxt = response.Content.ReadAsStringAsync().Result;
                                tempTxt = $"{tempTxt}";
                                var tempTracks = JsonConvert.DeserializeObject<List<Track>>(tempTxt);
                                foreach(var track in tempTracks)
                                {
                                    track.ServerURL = con.URL;
                                }
                                tracks.AddRange(tempTracks);

                            }
                            catch (HttpRequestException e)
                            {
                                return new ObjectResult(e.Message) { StatusCode = 500 };
                            }
                        }

                    }
                    catch (Exception)
                    {
                        
                    }
                }
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            var trackFilter = Builders<Track>.Filter.Regex("TrackName", new BsonRegularExpression(trackName, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Format", new BsonRegularExpression(format, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Bitrate", new BsonRegularExpression(bitrate, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("SampleRate", new BsonRegularExpression(sampleRate, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Channels", new BsonRegularExpression(channels, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("BitsPerSample", new BsonRegularExpression(bitsPerSample, "i"));
                trackFilter = trackFilter & Builders<Track>.Filter.Regex("Year", new BsonRegularExpression(year, "i"));

            // Play Count
            if (gtPlayCount != 0)
            {
                trackFilter = trackFilter & Builders<Track>.Filter.Gt("PlayCount", gtPlayCount);
            }
            
            if (ltPlayCount != 0)
            {
                trackFilter = trackFilter & Builders<Track>.Filter.Lt("PlayCount", ltPlayCount);
            }

            // Skip Count
            if (gtSkipCount != 0)
            {
                trackFilter = trackFilter & Builders<Track>.Filter.Gt("SkipCount", gtSkipCount);
            }
            
            if (ltSkipCount != 0)
            {
                trackFilter = trackFilter & Builders<Track>.Filter.Lt("SkipCount", ltSkipCount);
            }

            // Ratings
            if (gtRating != 0)
            {
                trackFilter = trackFilter & Builders<Track>.Filter.Gt("Rating", gtRating);
            }

            if (ltRating != 0)
            {
                trackFilter = trackFilter & Builders<Track>.Filter.Lt("Rating", ltRating);
            }

            // Date
            if (ltYear != 0 && ltMonth != 0 && ltDay != 0)
            {
                DateTime ltDateTime = new DateTime(ltYear, ltMonth, ltDay);
                trackFilter = trackFilter & Builders<Track>.Filter.Lt(x => x.ReleaseDate, ltDateTime);
            }

            if (gtYear != 0 && gtMonth != 0 && gtDay != 0)
            {
                DateTime gtDateTime = new DateTime(gtYear, gtMonth, gtDay);
                trackFilter = trackFilter & Builders<Track>.Filter.Gt(x => x.ReleaseDate, gtDateTime);
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    trackFilter = trackFilter & Builders<Track>.Filter.Regex("TrackGenres", new BsonRegularExpression(genre, "i"));
                }
            }

            var finalCount = count - ((Security.Connections.Count() + 1) * shortCount);

            var trackDocs = TracksCollection.Find(trackFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();
            tracks.AddRange(trackDocs);

            if(finalCount > 0)
            {
                tracks.AddRange(TracksCollection.Find(trackFilter)
                                            .Skip(page * count)
                                            .Limit(finalCount)
                                            .ToList());
            }


            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public IEnumerable<Album> SearchAlbums (int page, int count, string albumName = "", string publisher = "", string releaseType = "", string releaseStatus = "",
                                                long ltPlayCount = 0, long gtPlayCount = 0, long ltRating = 0, long gtRating = 0, int ltYear = 0, int ltMonth = 0, int ltDay = 0,
                                                int gtYear = 0, int gtMonth = 0, int gtDay = 0, string[] genres = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            var albumFilter = Builders<Album>.Filter.Regex("AlbumName", new BsonRegularExpression(albumName, "i"));
            
            albumFilter = albumFilter & Builders<Album>.Filter.Regex("Publisher", new BsonRegularExpression(publisher, "i"));
            albumFilter = albumFilter & Builders<Album>.Filter.Regex("ReleaseType", new BsonRegularExpression(releaseType, "i"));
            albumFilter = albumFilter & Builders<Album>.Filter.Regex("ReleaseStatus", new BsonRegularExpression(releaseStatus, "i"));
            

            // Play Count
            if (gtPlayCount != 0)
            {
                albumFilter = albumFilter & Builders<Album>.Filter.Gt("PlayCount", gtPlayCount);
            }

            if (ltPlayCount != 0)
            {
                albumFilter = albumFilter & Builders<Album>.Filter.Lt("PlayCount", ltPlayCount);
            }

            // Ratings
            if (gtRating != 0)
            {
                albumFilter = albumFilter & Builders<Album>.Filter.Gt("Rating", gtRating);
            }

            if (ltRating != 0)
            {
                albumFilter = albumFilter & Builders<Album>.Filter.Lt("Rating", ltRating);
            }

            // Date
            if (ltYear != 0 && ltMonth != 0 && ltDay != 0)
            {
                DateTime ltDateTime = new DateTime(ltYear, ltMonth, ltDay);
                albumFilter = albumFilter & Builders<Album>.Filter.Lt(x => x.ReleaseDate, ltDateTime);
            }

            if (gtYear != 0 && gtMonth != 0 && gtDay != 0)
            {
                DateTime gtDateTime = new DateTime(gtYear, gtMonth, gtDay);
                albumFilter = albumFilter & Builders<Album>.Filter.Gt(x => x.ReleaseDate, gtDateTime);
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    albumFilter = albumFilter & Builders<Album>.Filter.Regex("AlbumGenres", new BsonRegularExpression(genre, "i"));
                }
            }

            var albumDocs = AlbumCollection.Find(albumFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            foreach(var albumDoc in albumDocs)
            {
                albumDoc.Tracks = albumDoc.Tracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).ToList();
            }

            return albumDocs;
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public IEnumerable<Artist> SearchArtists(int page, int count, string ArtistName = "", long ltPlayCount = 0, long gtPlayCount = 0, long ltRating = 0, long gtRating = 0, string[] genres = null)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            var ArtistFilter = Builders<Artist>.Filter.Regex("ArtistName", new BsonRegularExpression(ArtistName, "i"));

            if (gtPlayCount != 0)
            {
                ArtistFilter = ArtistFilter & Builders<Artist>.Filter.Gt("PlayCount", gtPlayCount);
            }

            if (ltPlayCount != 0)
            {
                ArtistFilter = ArtistFilter & Builders<Artist>.Filter.Lt("PlayCount", ltPlayCount);
            }

            if (gtRating != 0)
            {
                ArtistFilter = ArtistFilter & Builders<Artist>.Filter.Gt("Rating", gtRating);
            }

            if (ltRating != 0)
            {
                ArtistFilter = ArtistFilter & Builders<Artist>.Filter.Lt("Rating", ltRating);
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    ArtistFilter = ArtistFilter & Builders<Artist>.Filter.Regex("Genres", new BsonRegularExpression(genre, "i"));
                }
            }

            var ArtistDocs = ArtistCollection.Find(ArtistFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            foreach (var artist in ArtistDocs)
            {
                try { artist.Tracks = artist.Tracks.OrderBy(x => x.ReleaseDate).ToList(); } catch (Exception) { }
                try { artist.Releases = artist.Releases.OrderBy(x => x.ReleaseDate).ToList(); } catch (Exception) { }
                try { artist.SeenOn = artist.SeenOn.OrderBy(x => x.ReleaseDate).ToList(); } catch (Exception) { }
            }

            return ArtistDocs;
        }

    }
}