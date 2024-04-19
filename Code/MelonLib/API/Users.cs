using Melon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class Users
    {
        public static async Task<ResponseUser> Current()
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/users/current");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    ResponseUser user = JsonSerializer.Deserialize<ResponseUser>(jsonResponse, options);

                    return user;
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
