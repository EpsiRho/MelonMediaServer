using Melon.Interface;
using Melon.Models;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace MelonLib.API
{
    /// <summary>
    /// Client calls for api/visuals: waveforms, FFTVIS frames, beat times and moments. Decode .fvz bytes with the
    /// FFTVIS library (<c>FFTVIS.AudioDecoder.Decode(stream)</c>).
    /// </summary>
    public static class Visuals
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        /// <summary>The server's settings as clients see them: enabled, default format, limits.</summary>
        public record ClientSettings(bool Enabled, VisualFormat Defaults, VisualLimits Limits);

        private static void EnsureReady()
        {
            if (string.IsNullOrEmpty(APIClient.BaseURL) || string.IsNullOrEmpty(APIClient.UserAgent))
                throw new ClientException("BaseURL or UserAgent not set!");
            if (string.IsNullOrEmpty(APIClient.JWT))
                throw new ClientException("Client is not authenticated.");
        }

        private static async Task<T?> GetJson<T>(string path, CancellationToken token)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}{path}", token);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>(Json, token);
        }

        public static Task<ClientSettings?> GetSettings(CancellationToken token = default) =>
            GetJson<ClientSettings>("api/visuals/settings", token);

        public static Task<float[]?> GetWaveform(string id, int width, CancellationToken token = default) =>
            GetJson<float[]>($"api/visuals/waveform?id={Uri.EscapeDataString(id)}&width={width}", token);

        public static Task<BeatTimes?> GetBeatTimes(string id, CancellationToken token = default) =>
            GetJson<BeatTimes>($"api/visuals/beat-times?id={Uri.EscapeDataString(id)}", token);

        public static Task<Moments?> GetMoments(string id, CancellationToken token = default) =>
            GetJson<Moments>($"api/visuals/moments?id={Uri.EscapeDataString(id)}", token);

        /// <summary>The track's .fvz bytes in the requested format (null fields use the server default), or null on failure.</summary>
        public static async Task<byte[]?> GetFvz(string id, VisualFormatRequest? format = null, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.GetAsync($"{APIClient.BaseURL}api/visuals/fvz?id={Uri.EscapeDataString(id)}{FormatQuery(format)}", token);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(token) : null;
        }

        /// <summary>Asks the server to prepare upcoming tracks. Returns how many were queued, or -1 on failure.</summary>
        public static async Task<int> Prefetch(string[] ids, VisualFormatRequest? format = null, string[]? include = null, CancellationToken token = default)
        {
            EnsureReady();
            using var response = await APIClient.RequestClient.PostAsJsonAsync($"{APIClient.BaseURL}api/visuals/prefetch",
                new VisualPrefetchRequest(ids, include, format), Json, token);
            if (!response.IsSuccessStatusCode) return -1;
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
            return doc.RootElement.TryGetProperty("queued", out var q) ? q.GetInt32() : 0;
        }

        /// <summary>Query string for a format request, e.g. "&amp;bands=16&amp;fps=30".</summary>
        public static string FormatQuery(VisualFormatRequest? f)
        {
            if (f == null) return "";
            var parts = new List<string>();
            void Add(string name, object? value)
            {
                if (value == null) return;
                var text = value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
                parts.Add($"{name}={Uri.EscapeDataString(text!.ToLowerInvariant() is "true" or "false" ? text.ToLowerInvariant() : text)}");
            }
            Add("bands", f.Bands); Add("fps", f.Fps); Add("fftResolution", f.FftResolution); Add("mapping", f.Mapping);
            Add("dbFloor", f.DbFloor); Add("dbRange", f.DbRange); Add("frequencyMin", f.FrequencyMin); Add("frequencyMax", f.FrequencyMax);
            Add("smoothness", f.Smoothness); Add("stereo", f.Stereo); Add("quantizeBits", f.QuantizeBits);
            Add("decayPerSecond", f.DecayPerSecond); Add("codec", f.Codec);
            return parts.Count == 0 ? "" : "&" + string.Join("&", parts);
        }
    }
}
