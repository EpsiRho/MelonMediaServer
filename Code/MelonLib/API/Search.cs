using Melon.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class Search
    {
        public static async Task<List<ResponseTrack>> Tracks(int page = 0, int count = 100, string trackName = "", string format = "", string bitrate = "",
                                                string sampleRate = "", string channels = "", string bitsPerSample = "", string year = "",
                                                long ltPlayCount = -1, long gtPlayCount = -1, long ltSkipCount = -1, long gtSkipCount = -1, 
                                                int ltYear = -1, int ltMonth = -1, int ltDay = -1,
                                                int gtYear = -1, int gtMonth = -1, int gtDay = -1, 
                                                long ltRating = -1, long gtRating = -1, List<string> genres = null,
                                                bool searchOr = false, string sort = "NameAsc", string albumName = "", string artistName = "")
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
            {
                throw new ClientException("Run APIClient.Init()");
            }

            if (string.IsNullOrEmpty(APIClient.JWT))
            {
                throw new ClientException("Client is not authenticated.");
            }

            string url = $"{APIClient.BaseURL}api/search/tracks?";
            url += $"page={page}&";
            url += $"count={count}&";
            url += $"trackName={trackName}&";
            url += $"format={format}&";
            url += $"bitrate={bitrate}&";
            url += $"sampleRate={sampleRate}&";
            url += $"channels={channels}&";
            url += $"bitsPerSample={bitsPerSample}&";
            url += $"ltPlayCount={ltPlayCount}&";
            url += $"gtPlayCount={gtPlayCount}&";
            url += $"ltSkipCount={ltSkipCount}&";
            url += $"gtSkipCount={gtSkipCount}&";
            url += $"ltYear={ltYear}&";
            url += $"ltMonth={ltMonth}&";
            url += $"ltDay={ltDay}&";
            url += $"gtYear={gtYear}&";
            url += $"gtMonth={gtMonth}&";
            url += $"gtDay={gtDay}&";
            url += $"gtMonth={gtMonth}&";
            url += $"gtMonth={ltRating}&";
            url += $"gtMonth={gtRating}&";
            url += $"searchOr={searchOr}&";
            url += $"sort={sort}&";
            url += $"albumName={albumName}&";
            url += $"artistName={artistName}&";
            if (genres != null)
            {
                foreach (var g in genres)
                {
                    url += $"genres={g}&";
                }
            }
            url = url.Substring(0, url.Length-1);

            try
            {
                var response = await APIClient.RequestClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

                    return tracks;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static async Task<List<ResponseAlbum>> Albums(int page = 0, int count = 100, string albumName = "", string publisher = "", 
                                                            string releaseType = "", string releaseStatus = "",
                                                            long ltPlayCount = -1, long gtPlayCount = -1, long ltRating = -1, long gtRating = -1, 
                                                            int ltYear = -1, int ltMonth = -1, int ltDay = -1,
                                                            int gtYear = -1, int gtMonth = -1, int gtDay = -1, 
                                                            List<string> genres = null, bool searchOr = false, string sort = "NameAsc")
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
            {
                throw new ClientException("Run APIClient.Init()");
            }

            if (string.IsNullOrEmpty(APIClient.JWT))
            {
                throw new ClientException("Client is not authenticated.");
            }

            string url = $"{APIClient.BaseURL}api/search/albums?";
            url += $"page={page}&";
            url += $"count={count}&";
            url += $"albumName={albumName}&";
            url += $"publisher={publisher}&";
            url += $"releaseType={releaseType}&";
            url += $"releaseStatus={releaseStatus}&";
            url += $"ltPlayCount={ltPlayCount}&";
            url += $"gtPlayCount={gtPlayCount}&";
            url += $"ltYear={ltYear}&";
            url += $"ltMonth={ltMonth}&";
            url += $"ltDay={ltDay}&";
            url += $"gtYear={gtYear}&";
            url += $"gtMonth={gtMonth}&";
            url += $"gtDay={gtDay}&";
            url += $"gtMonth={gtMonth}&";
            url += $"gtMonth={ltRating}&";
            url += $"gtMonth={gtRating}&";
            url += $"searchOr={searchOr}&";
            url += $"sort={sort}&";
            if (genres != null)
            {
                foreach (var g in genres)
                {
                    url += $"genres={g}&";
                }
            }
            url = url.Substring(0, url.Length - 1);

            try
            {
                var response = await APIClient.RequestClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseAlbum> albums = JsonSerializer.Deserialize<List<ResponseAlbum>>(jsonResponse, options);

                    return albums;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static async Task<List<ResponseArtist>> Artists(int page = 0, int count = 100, string artistName = "", 
                                                             long ltPlayCount = -1, long gtPlayCount = -1, long ltRating = -1,
                                                             long gtRating = -1, List<string> genres = null, bool searchOr = false, string sort = "NameAsc")
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
            {
                throw new ClientException("Run APIClient.Init()");
            }

            if (string.IsNullOrEmpty(APIClient.JWT))
            {
                throw new ClientException("Client is not authenticated.");
            }

            string url = $"{APIClient.BaseURL}api/search/artists?";
            url += $"page={page}&";
            url += $"count={count}&";
            url += $"artistName={artistName}&";
            url += $"ltPlayCount={ltPlayCount}&";
            url += $"gtPlayCount={gtPlayCount}&";
            url += $"gtMonth={ltRating}&";
            url += $"gtMonth={gtRating}&";
            url += $"searchOr={searchOr}&";
            url += $"sort={sort}&";
            if (genres != null)
            {
                foreach (var g in genres)
                {
                    url += $"genres={g}&";
                }
            }
            url = url.Substring(0, url.Length - 1);

            try
            {
                var response = await APIClient.RequestClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseArtist> artists = JsonSerializer.Deserialize<List<ResponseArtist>>(jsonResponse, options);

                    return artists;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
