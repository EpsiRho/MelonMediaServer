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
    public static class Queues
    {
        public static async Task<string> Create(string name, List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
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
                string str = $"{APIClient.BaseURL}api/queues/create?shuffle={shuffle}&enableTrackLinks={enableTrackLinks}&name={name}";
                foreach(var id in ids)
                {
                    str += $"&ids={id}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        public static async Task<string> CreateFromAlbums(string name, List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
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
                string str = $"{APIClient.BaseURL}api/queues/create-from-albums?shuffle={shuffle}&enableTrackLinks={enableTrackLinks}&name={name}";
                foreach(var id in ids)
                {
                    str += $"&ids={id}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
        public static async Task<string> CreateFromArtists(string name, List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
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
                string str = $"{APIClient.BaseURL}api/queues/create-from-artists?shuffle={shuffle}&enableTrackLinks={enableTrackLinks}&name={name}";
                foreach(var id in ids)
                {
                    str += $"&ids={id}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
        public static async Task<string> CreateFromPlaylists(string name, List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
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
                string str = $"{APIClient.BaseURL}api/queues/create-from-playlists?shuffle={shuffle}&enableTrackLinks={enableTrackLinks}&name={name}";
                foreach(var id in ids)
                {
                    str += $"&ids={id}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
        public static async Task<string> CreateFromCollections(string name, List<string> ids, string shuffle = "none", bool enableTrackLinks = true)
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
                string str = $"{APIClient.BaseURL}api/queues/create-from-collections?shuffle={shuffle}&enableTrackLinks={enableTrackLinks}&name={name}";
                foreach(var id in ids)
                {
                    str += $"&ids={id}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static async Task<ResponseQueue> GetQueueFromId(string id)
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
                string str = $"{APIClient.BaseURL}api/queues/get?id={id}";
                var response = await APIClient.RequestClient.GetAsync(str);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    ResponseQueue queue = JsonSerializer.Deserialize<ResponseQueue>(jsonResponse, options);

                    return queue;
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
        public static async Task<List<ResponseQueue>> Search(int page = 0, int count = 100, string name = "", bool sortByLastListen = true)
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
                string str = $"{APIClient.BaseURL}api/queues/search?page={page}&count={count}&name={name}&sortByLastListen={sortByLastListen}";
                var response = await APIClient.RequestClient.GetAsync(str);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseQueue> queues = JsonSerializer.Deserialize<List<ResponseQueue>>(jsonResponse, options);

                    return queues;
                }
                else
                {
                    return new List<ResponseQueue>() { new ResponseQueue() { Name = await response.Content.ReadAsStringAsync()} };
                }
            }
            catch (Exception e)
            {
                return new List<ResponseQueue>() { new ResponseQueue() { Name = e.Message } };
            }
        }
        public static async Task<List<ResponseTrack>> GetTracks(int page = 0, int count = 100, string id = "")
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
                string str = $"{APIClient.BaseURL}api/queues/get-tracks?page={page}&count={count}&id={id}";
                var response = await APIClient.RequestClient.GetAsync(str);
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
                    return new List<ResponseTrack>() { new ResponseTrack() { Name = response.StatusCode.ToString() } };
                }
            }
            catch (Exception e)
            {
                return new List<ResponseTrack>() { new ResponseTrack() { Name = e.Message } };
            }
        }
        public static async Task<ResponseTrack> GetTrack(string id = "", int index = 0)
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
                string str = $"{APIClient.BaseURL}api/queues/get-track?index={index}&id={id}";
                var response = await APIClient.RequestClient.GetAsync(str);
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
            catch (Exception e)
            {
                return null;
            }
        }
        public static async Task<string> AddToQueue(string id, List<string> trackIds, string position = "end", int place = 0)
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
                string str = $"{APIClient.BaseURL}api/queues/add-tracks?id={id}&position={position}&place={place}";
                foreach (var trackId in trackIds)
                {
                    str += $"&trackIds={trackId}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return jsonResponse;
                }
                else
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return jsonResponse;
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        public static async Task<bool> RemoveFromQueue(string id, List<int> positions)
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
                string str = $"{APIClient.BaseURL}api/queues/remove-tracks?id={id}";
                foreach (var idx in positions)
                {
                    str += $"&positions={idx}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

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
        public static async Task<bool> DeleteQueue(string id)
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
                string str = $"{APIClient.BaseURL}api/queues/delete?id={id}";
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

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
        public static async Task<bool> MoveTrack(string id, int fromPos, int toPos)
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
                string str = $"{APIClient.BaseURL}api/queues/move-track?id={id}&fromPos={fromPos}&toPos={toPos}";
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

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
        public static async Task<bool> UpdateQueuePosition(string id, int pos, string device = "")
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
                string str = $"{APIClient.BaseURL}api/queues/update-position?id={id}&pos={pos}&device={device}";
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

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
        public static async Task<bool> UpdateQueue(string id, string name = "",List<string> editors = null, List<string> viewers = null,
                                                   string publicEditing = "", string publicViewing = "")
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
                string str = $"{APIClient.BaseURL}api/queues/update?id={id}&name={name}&publicEditing={publicEditing}&publicViewing={publicViewing}";
                foreach (var editor in editors)
                {
                    str += $"&editors={editor}";
                }
                foreach (var viewer in viewers)
                {
                    str += $"&viewers={viewer}";
                }
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

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
        public static async Task<bool> Shuffle(string id, string shuffle = "None", bool enableTrackLinks = true)
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
                string str = $"{APIClient.BaseURL}api/queues/shuffle?id={id}&shuffle={shuffle}&enableTrackLinks={enableTrackLinks}&";
                var response = await APIClient.RequestClient.PostAsync(str, null);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    List<ResponseTrack> tracks = JsonSerializer.Deserialize<List<ResponseTrack>>(jsonResponse, options);

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
