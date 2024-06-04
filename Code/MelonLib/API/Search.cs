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
        public static async Task<List<ResponseTrack>> Tracks(int page = 0, int count = 100, List<string> andFilters = null, List<string> orFilters = null, string sort = "NameAsc")
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
            url += $"sort={sort}";
            if (andFilters != null)
            {
                foreach (var f in andFilters)
                {
                    url += $"&andFilters={f}";
                }
            }
            if (orFilters != null)
            {
                foreach (var f in orFilters)
                {
                    url += $"&orFilters={f}";
                }
            }

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
        public static async Task<List<ResponseAlbum>> Albums(int page = 0, int count = 100, List<string> andFilters = null, List<string> orFilters = null, string sort = "NameAsc")
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
            url += $"sort={sort}";
            if (andFilters != null)
            {
                foreach (var f in andFilters)
                {
                    url += $"&andFilters={f}";
                }
            }
            if (orFilters != null)
            {
                foreach (var f in orFilters)
                {
                    url += $"&orFilters={f}";
                }
            }

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
        public static async Task<List<ResponseArtist>> Artists(int page = 0, int count = 100, List<string> andFilters = null, List<string> orFilters = null, string sort = "NameAsc")
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
            url += $"sort={sort}";
            if (andFilters != null)
            {
                foreach (var f in andFilters)
                {
                    url += $"&andFilters={f}";
                }
            }
            if (orFilters != null)
            {
                foreach (var f in orFilters)
                {
                    url += $"&orFilters={f}";
                }
            }

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
