using Melon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
                // Session sign-in: the password goes once in the Basic header; the access token lasts 15 minutes.
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{APIClient.BaseURL}auth/token")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { deviceName = APIClient.UserAgent }), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
                var response = await APIClient.RequestClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    APIClient.JWT = doc.RootElement.GetProperty("accessToken").GetString();
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
        public static async Task<int> CheckAuth()
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}check");
                if (response.IsSuccessStatusCode)
                {
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
