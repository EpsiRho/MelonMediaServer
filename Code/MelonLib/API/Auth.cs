using Melon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class Auth
    {
        public static async Task<int> AuthenticateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
            {
                throw new ClientException("BaseURL or UserAgent not set!");
            }

            try
            {
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}auth/login?username={username}&password={password}");
                if (response.IsSuccessStatusCode)
                {
                    APIClient.JWT = response.Content.ReadAsStringAsync().Result.Replace("\"", "");
                    APIClient.RequestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", APIClient.JWT);
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
            catch (Exception)
            {
                return 2;
            }
        }
    }
}
