using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class APIClient
    {
        public static string UserAgent { get; set; }
        public static string JWT { get; set; }
        public static string _baseURL;
        public static string BaseURL
        {
            get
            {
                return _baseURL;
            }
            set
            {
                if(value == _baseURL)
                {
                    return;
                }

                if (value.EndsWith('/'))
                {
                    _baseURL = value;
                }
                else
                {
                    _baseURL = $"{value}/";
                }
            }
        }
        public static HttpClient RequestClient { get; set; }
        public static async Task<bool> Init(string _baseURL, string userAgent, bool debug)
        {
            BaseURL = _baseURL;
            UserAgent = userAgent;

            if (string.IsNullOrEmpty(BaseURL) || string.IsNullOrEmpty(UserAgent))
            {
                return false;
            }

            // If debug set, disable the need for SSL match. Should only be in use for testing a server from a localhost.
            var handler = new HttpClientHandler();
            if (debug)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) =>
                    {
                        return true;
                    };
            }

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            client.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                var response = await client.GetAsync($"{BaseURL}auth/login?username=NA&password=NA");
                RequestClient = client;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
