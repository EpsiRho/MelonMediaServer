using Melon.Models;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace MelonLib.API
{
    /// <summary>
    /// Client calls for api/eq (saved EQ filters). Show filters as <see cref="ResponseEqFilter.Name"/> with
    /// <see cref="ResponseEqFilter.OwnerName"/>; to play one, use <see cref="TrackUrl"/> or <see cref="TranscodeUrl"/>.
    /// </summary>
    public static class Eq
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private static void EnsureReady()
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
                throw new ClientException("BaseURL or UserAgent not set!");
            if (string.IsNullOrEmpty(APIClient.JWT))
                throw new ClientException("Client is not authenticated.");
        }

        private static string Q(string value) => Uri.EscapeDataString(value ?? "");

        /// <summary>Throws a <see cref="ClientException"/> carrying the server's message for a failed request.</summary>
        private static async Task Check(HttpResponseMessage response, CancellationToken token)
        {
            if (response.IsSuccessStatusCode) return;
            var body = await response.Content.ReadAsStringAsync(token);
            throw new ClientException(string.IsNullOrWhiteSpace(body) ? $"Request failed ({(int)response.StatusCode})." : body.Trim('"'));
        }

        /// <summary>The user's own filters plus public ones (or only their own), sorted by name.</summary>
        public static async Task<List<ResponseEqFilter>> List(bool mineOnly = false, string name = "", int page = 0, int count = 100, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.GetAsync(
                $"{APIClient.BaseURL}api/eq/list?page={page}&count={count}&mine={(mineOnly ? "true" : "false")}&name={Q(name)}", token);
            await Check(response, token);
            return await response.Content.ReadFromJsonAsync<List<ResponseEqFilter>>(Json, token) ?? [];
        }

        /// <summary>One filter, or null if it doesn't exist or isn't visible to the user.</summary>
        public static async Task<ResponseEqFilter?> Get(string id, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/eq/get?id={Q(id)}", token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            await Check(response, token);
            return await response.Content.ReadFromJsonAsync<ResponseEqFilter>(Json, token);
        }

        /// <summary>Saves a new filter and returns its id.</summary>
        public static async Task<string> Add(EqFilterRequest filter, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.PostAsJsonAsync($"{APIClient.BaseURL}api/eq/add", filter, Json, token);
            await Check(response, token);
            return await response.Content.ReadFromJsonAsync<string>(Json, token) ?? "";
        }

        /// <summary>Replaces one of the user's filters (IsPublic too, when set).</summary>
        public static async Task Update(string id, EqFilterRequest filter, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.PostAsJsonAsync($"{APIClient.BaseURL}api/eq/update?id={Q(id)}", filter, Json, token);
            await Check(response, token);
        }

        public static async Task SetPublic(string id, bool isPublic, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.PostAsync(
                $"{APIClient.BaseURL}api/eq/set-public?id={Q(id)}&isPublic={(isPublic ? "true" : "false")}", null, token);
            await Check(response, token);
        }

        public static async Task Delete(string id, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.DeleteAsync($"{APIClient.BaseURL}api/eq/delete?id={Q(id)}", token);
            await Check(response, token);
        }

        /// <summary>A saved filter's response curve (dB per frequency) for drawing.</summary>
        public static async Task<EqResponse?> GetResponse(string id, int points = 256, int sampleRate = 44100, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.GetAsync(
                $"{APIClient.BaseURL}api/eq/response?id={Q(id)}&points={points}&sampleRate={sampleRate}", token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            await Check(response, token);
            return await response.Content.ReadFromJsonAsync<EqResponse>(Json, token);
        }

        /// <summary>The response curve of unsaved settings, for previewing while editing.</summary>
        public static async Task<EqResponse?> PreviewResponse(EqFilterRequest filter, int points = 256, int sampleRate = 44100, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.PostAsJsonAsync(
                $"{APIClient.BaseURL}api/eq/response?points={points}&sampleRate={sampleRate}", filter, Json, token);
            await Check(response, token);
            return await response.Content.ReadFromJsonAsync<EqResponse>(Json, token);
        }

        /// <summary>URL of the track with the filter applied, as a WAV that streams with Range support.</summary>
        public static string TrackUrl(string trackId, string eqId) =>
            $"{APIClient.BaseURL}api/download/track?id={Q(trackId)}&eq={Q(eqId)}";

        /// <summary>URL of the track as MP3 at <paramref name="kbps"/> with the filter applied (cached per filter version).</summary>
        public static string TranscodeUrl(string trackId, string eqId, int kbps = 256) =>
            $"{APIClient.BaseURL}api/download/track-transcode?id={Q(trackId)}&transcodeBitrate={kbps.ToString(CultureInfo.InvariantCulture)}&eq={Q(eqId)}";
    }
}
