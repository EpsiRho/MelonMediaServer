using Melon.Models;
using MelonLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class Stats
    {
        public static async Task<bool> LogPlay(string id, string device="", string dateTime="")
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
                var response = await APIClient.RequestClient.PostAsync($"{APIClient.BaseURL}api/stats/log-play?id={id}&device={device}&dateTime={dateTime}", null);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static async Task<bool> RateTrack(string id, double rating)
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
                var response = await APIClient.RequestClient.PostAsync($"{APIClient.BaseURL}api/stats/rate-track?id={id}&rating={rating}", null);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static async Task<List<ResponseTrackStat>> RecentTracks(string userId="", string device="",int page = 0, int count = 100)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/stats/recent-tracks?userId={userId}&device={device}&page={page}&count={count}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrackStat> tracks = JsonSerializer.Deserialize<List<ResponseTrackStat>>(jsonResponse, options);

                    return tracks;
                }
                else
                {
                    return new List<ResponseTrackStat>();
                }
            }
            catch (Exception)
            {
                return new List<ResponseTrackStat>();
            }
        }
    }
}
