using Melon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MelonLib.API
{
    public static class ItemInfo
    {
        public static async Task<ResponseTrack> GetTrack(string id)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/track?id={id}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    ResponseTrack track = JsonSerializer.Deserialize<ResponseTrack>(jsonResponse, options);

                    return track;
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
        public static async Task<ResponseAlbum> GetAlbum(string id)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/album?id={id}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    ResponseAlbum album = JsonSerializer.Deserialize<ResponseAlbum>(jsonResponse, options);

                    return album;
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
        public static async Task<ResponseArtist> GetArtist(string id)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/artist?id={id}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    ResponseArtist artist = JsonSerializer.Deserialize<ResponseArtist>(jsonResponse, options);

                    return artist;
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


        public static async Task<List<ResponseTrack>> GetAlbumTracks(string id, int page = 0, int count = 100)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/album/tracks?id={id}&page={page}&count={count}");
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
        public static async Task<List<ResponseTrack>> GetArtistTracks(string id, int page = 0, int count = 100)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/artist/tracks?id={id}&page={page}&count={count}");
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
        public static async Task<List<ResponseAlbum>> GetArtistReleases(string id, int page = 0, int count = 100)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/artist/releases?id={id}&page={page}&count={count}");
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
        public static async Task<List<ResponseAlbum>> GetArtistSeenOn(string id, int page = 0, int count = 100)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/artist/seen-on?id={id}&page={page}&count={count}");
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
        public static async Task<List<ResponseArtist>> GetArtistConnections(string id, int page = 0, int count = 100)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/artist/connections?id={id}&page={page}&count={count}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseArtist> artist = JsonSerializer.Deserialize<List<ResponseArtist>>(jsonResponse, options);

                    return artist;
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
        public static async Task<string> GetTrackLyrics(string id)
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
                var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/lyrics?id={id}");
                if (response.IsSuccessStatusCode)
                {
                    var restxt = await response.Content.ReadAsStringAsync();
                    return restxt;
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
