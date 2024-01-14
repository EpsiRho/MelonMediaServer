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
using System;
using Humanizer.Localisation;

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
        public ObjectResult SearchTracks(int page = 0, int count = 100, string trackName = "", string format = "", string bitrate = "", 
                                               string sampleRate = "", string channels = "", string bitsPerSample = "", string year = "", 
                                               long ltPlayCount = 0, long gtPlayCount = 0, long ltSkipCount = 0, long gtSkipCount = 0, int ltYear = 0, int ltMonth = 0, int ltDay = 0,
                                               int gtYear = 0, int gtMonth = 0, int gtDay = 0, long ltRating = 0, long gtRating = 0, [FromQuery] string[] genres = null, 
                                               bool externalResults = false, bool searchOr = false)
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
                            sURL += $"&searchOr={searchOr}";
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

                    }
                    catch (Exception)
                    {

                    }
                }
            }

            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var TracksCollection = mongoDatabase.GetCollection<Track>("Tracks");

            List<FilterDefinition<Track>> filterList = new List<FilterDefinition<Track>>();
            if (trackName != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("TrackName", new BsonRegularExpression(trackName, "i")));
            }
            else if (trackName == "" && !searchOr)
            {
                filterList.Add(Builders<Track>.Filter.Regex("TrackName", new BsonRegularExpression(trackName, "i")));
            }

            if (format != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("Format", new BsonRegularExpression(format, "i")));
            }

            if (bitrate != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("Bitrate", new BsonRegularExpression(bitrate, "i")));
            }

            if (sampleRate != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("SampleRate", new BsonRegularExpression(sampleRate, "i")));
            }

            if (channels != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("Channels", new BsonRegularExpression(channels, "i")));
            }

            if (bitsPerSample != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("BitsPerSample", new BsonRegularExpression(bitsPerSample, "i")));
            }

            if (year != "")
            {
                filterList.Add(Builders<Track>.Filter.Regex("Year", new BsonRegularExpression(year, "i")));
            }

            // Play Count
            if (gtPlayCount != 0)
            {
                filterList.Add(Builders<Track>.Filter.Gt("PlayCount", gtPlayCount));
            }
            
            if (ltPlayCount != 0)
            {
                filterList.Add(Builders<Track>.Filter.Lt("PlayCount", ltPlayCount));
            }

            // Skip Count
            if (gtSkipCount != 0)
            {
                filterList.Add(Builders<Track>.Filter.Gt("SkipCount", gtSkipCount));
            }
            
            if (ltSkipCount != 0)
            {
                filterList.Add(Builders<Track>.Filter.Lt("SkipCount", ltSkipCount));
            }

            // Ratings
            if (gtRating != 0)
            {
                filterList.Add(Builders<Track>.Filter.Gt("Rating", gtRating));
            }

            if (ltRating != 0)
            {
                filterList.Add(Builders<Track>.Filter.Lt("Rating", ltRating));
            }

            // Date
            if (ltYear != 0 && ltMonth != 0 && ltDay != 0)
            {
                DateTime ltDateTime = new DateTime(ltYear, ltMonth, ltDay);
                filterList.Add(Builders<Track>.Filter.Lt(x => x.ReleaseDate, ltDateTime));
            }

            if (gtYear != 0 && gtMonth != 0 && gtDay != 0)
            {
                DateTime gtDateTime = new DateTime(gtYear, gtMonth, gtDay);
                filterList.Add(Builders<Track>.Filter.Gt(x => x.ReleaseDate, gtDateTime));
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    filterList.Add(Builders<Track>.Filter.Regex("TrackGenres", new BsonRegularExpression(genre, "i")));
                }
            }

            FilterDefinition<Track> combinedFilter = null;
            foreach(var filter in filterList)
            {
                if(combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    if (searchOr)
                    {
                        combinedFilter = Builders<Track>.Filter.Or(combinedFilter, filter);
                    }
                    else
                    {
                        combinedFilter = Builders<Track>.Filter.And(combinedFilter, filter);
                    }
                }
            }

            var finalCount = count - ((Security.Connections.Count() + 1) * shortCount);

            var trackDocs = TracksCollection.Find(combinedFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();
            tracks.AddRange(trackDocs);

            if(finalCount > 0)
            {
                tracks.AddRange(TracksCollection.Find(combinedFilter)
                                            .Skip(page * count)
                                            .Limit(finalCount)
                                            .ToList());
            }


            return new ObjectResult(tracks) { StatusCode = 200 };
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("albums")]
        public IEnumerable<Album> SearchAlbums (int page = 0, int count = 100, string albumName = "", string publisher = "", string releaseType = "", string releaseStatus = "",
                                                long ltPlayCount = 0, long gtPlayCount = 0, long ltRating = 0, long gtRating = 0, int ltYear = 0, int ltMonth = 0, int ltDay = 0,
                                                int gtYear = 0, int gtMonth = 0, int gtDay = 0, [FromQuery] string[] genres = null, bool searchOr = false)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);

            var mongoDatabase = mongoClient.GetDatabase("Melon");

            var AlbumCollection = mongoDatabase.GetCollection<Album>("Albums");

            List<FilterDefinition<Album>> filterList = new List<FilterDefinition<Album>>();

            if (albumName != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex("AlbumName", new BsonRegularExpression(albumName, "i")));
            }
            else if (albumName == "" && !searchOr)
            {
                filterList.Add(Builders<Album>.Filter.Regex("AlbumName", new BsonRegularExpression(albumName, "i")));
            }

            if (publisher != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex("Publisher", new BsonRegularExpression(publisher, "i")));
            }

            if (releaseType != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex("ReleaseType", new BsonRegularExpression(releaseType, "i")));
            }

            if (releaseType != "")
            {
                filterList.Add(Builders<Album>.Filter.Regex("ReleaseStatus", new BsonRegularExpression(releaseStatus, "i")));
            }
            

            // Play Count
            if (gtPlayCount != 0)
            {
                filterList.Add(Builders<Album>.Filter.Gt("PlayCount", gtPlayCount));
            }

            if (ltPlayCount != 0)
            {
                filterList.Add(Builders<Album>.Filter.Lt("PlayCount", ltPlayCount));
            }

            // Ratings
            if (gtRating != 0)
            {
                filterList.Add(Builders<Album>.Filter.Gt("Rating", gtRating));
            }

            if (ltRating != 0)
            {
                filterList.Add(Builders<Album>.Filter.Lt("Rating", ltRating));
            }

            // Date
            if (ltYear != 0 && ltMonth != 0 && ltDay != 0)
            {
                DateTime ltDateTime = new DateTime(ltYear, ltMonth, ltDay);
                filterList.Add(Builders<Album>.Filter.Lt(x => x.ReleaseDate, ltDateTime));
            }

            if (gtYear != 0 && gtMonth != 0 && gtDay != 0)
            {
                DateTime gtDateTime = new DateTime(gtYear, gtMonth, gtDay);
                filterList.Add(Builders<Album>.Filter.Gt(x => x.ReleaseDate, gtDateTime));
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    filterList.Add(Builders<Album>.Filter.Regex("AlbumGenres", new BsonRegularExpression(genre, "i")));
                }
            }

            FilterDefinition<Album> combinedFilter = null;
            foreach (var filter in filterList)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    if (searchOr)
                    {
                        combinedFilter = Builders<Album>.Filter.Or(combinedFilter, filter);
                    }
                    else
                    {
                        combinedFilter = Builders<Album>.Filter.And(combinedFilter, filter);
                    }
                }
            }

            var albumDocs = AlbumCollection.Find(combinedFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();


            //foreach (var albumDoc in albumDocs)
            //{
            //    albumDoc.Tracks = albumDoc.Tracks.OrderBy(x => x.Disc).ThenBy(x => x.Position).ToList();
            //}

            return albumDocs;
        }
        [Authorize(Roles = "Admin,User,Pass")]
        [HttpGet("artists")]
        public IEnumerable<Artist> SearchArtists(int page = 0, int count = 100, string artistName = "", long ltPlayCount = 0, long gtPlayCount = 0, long ltRating = 0, 
                                                 long gtRating = 0, [FromQuery] string[] genres = null, bool searchOr = false)
        {
            var mongoClient = new MongoClient(StateManager.MelonSettings.MongoDbConnectionString);
            var mongoDatabase = mongoClient.GetDatabase("Melon");
            var ArtistCollection = mongoDatabase.GetCollection<Artist>("Artists");

            List<FilterDefinition<Artist>> filterList = new List<FilterDefinition<Artist>>();
            if (artistName != "")
            {
                filterList.Add(Builders<Artist>.Filter.Regex("ArtistName", new BsonRegularExpression(artistName, "i")));
            }
            else if (artistName == "" && !searchOr)
            {
                filterList.Add(Builders<Artist>.Filter.Regex("ArtistName", new BsonRegularExpression(artistName, "i")));
            }

            if (gtPlayCount != 0)
            {
                filterList.Add(Builders<Artist>.Filter.Gt("PlayCount", gtPlayCount));
            }

            if (ltPlayCount != 0)
            {
                filterList.Add(Builders<Artist>.Filter.Lt("PlayCount", ltPlayCount));
            }

            if (gtRating != 0)
            {
                filterList.Add(Builders<Artist>.Filter.Gt("Rating", gtRating));
            }

            if (ltRating != 0)
            {
                filterList.Add(Builders<Artist>.Filter.Lt("Rating", ltRating));
            }

            if (genres != null)
            {
                foreach (var genre in genres)
                {
                    filterList.Add(Builders<Artist>.Filter.Regex("Genres", new BsonRegularExpression(genre, "i")));
                }
            }

            FilterDefinition<Artist> combinedFilter = null;
            foreach (var filter in filterList)
            {
                if (combinedFilter == null)
                {
                    combinedFilter = filter;
                }
                else
                {
                    if (searchOr)
                    {
                        combinedFilter = Builders<Artist>.Filter.Or(combinedFilter, filter);
                    }
                    else
                    {
                        combinedFilter = Builders<Artist>.Filter.And(combinedFilter, filter);
                    }
                }
            }

            var ArtistDocs = ArtistCollection.Find(combinedFilter)
                                            .Skip(page * count)
                                            .Limit(count)
                                            .ToList();

            //foreach (var artist in ArtistDocs)
            //{
            //    try { artist.Tracks = artist.Tracks.OrderBy(x => x.ReleaseDate).ToList(); } catch (Exception) { }
            //    try { artist.Releases = artist.Releases.OrderBy(x => x.ReleaseDate).ToList(); } catch (Exception) { }
            //    try { artist.SeenOn = artist.SeenOn.OrderBy(x => x.ReleaseDate).ToList(); } catch (Exception) { }
            //}

            return ArtistDocs;
        }

    }
}