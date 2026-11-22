namespace Nova.Dsp
{
    /// <summary>
    /// Triangular filters evenly spaced on the mel scale (1127.01048 · ln(1 + f/700)). Each covers [edge i, edge i+2) in
    /// rounded bins, is linear in mel, and is normalised to sum to 1. Shared by the AcousticBrainz mel bands (40 bands to
    /// 11 kHz) and Nova.Frames' mel input (64 bands to 16 kHz).
    /// </summary>
    public static class MelFilterbank
    {
        public static double ToMel(double hz) => 1127.01048 * Math.Log(hz / 700 + 1);

        public static double ToHz(double mel) => 700 * (Math.Exp(mel / 1127.01048) - 1);

        /// <param name="bins">Spectrum bins, frame size / 2 + 1.</param>
        /// <returns>Per band, its (bin, weight) pairs.</returns>
        public static (int bin, double weight)[][] Create(int sampleRate, int bins, int bands, double lowHz, double highHz)
        {
            var scale = sampleRate / 2.0 / (bins - 1);
            var step = (ToMel(highHz) - ToMel(lowHz)) / (bands + 1);
            var edges = Enumerable.Range(0, bands + 2).Select(k => ToHz(ToMel(lowHz) + k * step)).ToArray();
            var filters = new (int, double)[bands][];
            for (var b = 0; b < bands; b++)
            {
                var lowMel = ToMel(edges[b]);
                var centreMel = ToMel(edges[b + 1]);
                var highMel = ToMel(edges[b + 2]);
                var start = (int)Math.Round(edges[b] / scale, MidpointRounding.AwayFromZero);
                var end = Math.Min((int)Math.Round(edges[b + 2] / scale, MidpointRounding.AwayFromZero), bins);
                var weights = new List<(int, double)>();
                for (var k = start; k < end; k++)
                {
                    var mel = ToMel(k * scale);
                    var w = mel <= centreMel ? (mel - lowMel) / (centreMel - lowMel) : (highMel - mel) / (highMel - centreMel);
                    if (w > 0)
                    {
                        weights.Add((k, w));
                    }
                }
                var sum = weights.Sum(w => w.Item2);
                filters[b] = sum > 0 ? weights.Select(w => (w.Item1, w.Item2 / sum)).ToArray() : weights.ToArray();
            }
            return filters;
        }
    }
}
