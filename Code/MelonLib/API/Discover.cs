using Melon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class Discover
    {
        public static async Task<List<ResponseTrack>> Tracks(List<string> ids, bool orderByFavorites = false, bool orderByDiscovery = false, int count = 100,
                                                             bool enableTrackLinks = true, bool includeArtists = true, bool includeGenres = true)
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
            {
                throw new ClientException("BaseURL or UserAgent not set!");
            }

            if (string.IsNullOrEmpty(APIClient.JWT))
            {
                throw new ClientException("Client is not authenticated.");
            }

            try
            {
                string url = $"{APIClient.BaseURL}api/discover/tracks?orderByFavorites={orderByFavorites}&orderByDiscovery={orderByDiscovery}&count={count}&enableTrackLinks={enableTrackLinks}&includeArtists={includeArtists}&includeGenres={includeGenres}";
                foreach(var id in ids)
                {
                    url += $"&ids={id}";
                }
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
                    return new List<ResponseTrack>();
                }
            }
            catch (Exception)
            {
                return new List<ResponseTrack>();
            }
        }
    }
}
