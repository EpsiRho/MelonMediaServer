using Melon.Models;
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
    }
}
