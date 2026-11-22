using Nova.Dsp;

namespace Nova.Features
{
    /// <summary>
    /// Octave-like spectral contrast (Jiang et al., with Akkermans' band layout as in Essentia): per band, the
    /// contrast between the mean of its strongest and weakest bins, and the log of the weakest ("valley").
    /// AcousticBrainz uses 6 bands over 20–11000 Hz with 15% of the bins spread evenly and the rest exponentially.
    /// </summary>
    public sealed class SpectralContrast
    {
        private const double Tiny = 1e-30;

        private readonly int _startBin;
        private readonly int[] _bandSizes;
        private readonly double _neighbourRatio;

        public SpectralContrast(int frameSize, int sampleRate, int bandCount = 6, double lowFrequency = 20,
            double highFrequency = 11000, double neighbourRatio = 0.4, double staticDistribution = 0.15)
        {
            _neighbourRatio = neighbourRatio;
            var binWidth = (double)sampleRate / frameSize;
            var scaled = 1 - staticDistribution;
            var lastBins = (int)(lowFrequency / binWidth);
            _startBin = lastBins;
            var totalBins = (int)(highFrequency / binWidth);
            var upper = (int)(scaled * totalBins) * binWidth;
            var staticBins = (int)(staticDistribution * totalBins) / bandCount;
            var ratioPerBand = Math.Pow(upper / lowFrequency, 1.0 / bandCount);

            _bandSizes = new int[bandCount];
            var frequency = lowFrequency;
            for (var b = 0; b < bandCount; b++)
            {
                frequency *= ratioPerBand;
                _bandSizes[b] = (int)(frequency / binWidth - lastBins + staticBins);
                lastBins = (int)(frequency / binWidth);
            }
        }

        public int BandCount => _bandSizes.Length;

        public void Compute(ReadOnlySpan<double> spectrum, Span<double> contrast, Span<double> valleys)
        {
            var start = _startBin;
            var sorted = new double[_bandSizes.Max()];
            for (var b = 0; b < _bandSizes.Length && start < spectrum.Length; b++)
            {
                var size = _bandSizes[b];
                var available = Math.Min(size, spectrum.Length - start);
                var band = sorted.AsSpan(0, available);
                spectrum.Slice(start, available).CopyTo(band);

                var mean = 0.0;
                foreach (var v in band)
                {
                    mean += v;
                }
                mean = (size != 0 ? mean / size : mean) + Tiny;

                var neighbours = Math.Max(1, (int)(_neighbourRatio * size));
                double valley = 0, peak = 0;
                if (available == size && neighbours <= size)
                {
                    // Only the sums of the weakest and strongest bins matter, so selection replaces a full sort.
                    Selection.Select(band, neighbours - 1);
                    foreach (var v in band[..neighbours])
                    {
                        valley += v;
                    }
                    Selection.Select(band, size - neighbours);
                    foreach (var v in band[(size - neighbours)..])
                    {
                        peak += v;
                    }
                }
                else
                {
                    // A band cut short by the end of the spectrum: sort, and take what exists of each end.
                    band.Sort();
                    for (var i = 0; i < neighbours && i < available; i++)
                    {
                        valley += band[i];
                    }
                    for (var i = size; i > size - neighbours && i > 0; i--)
                    {
                        if (i - 1 < available)
                        {
                            peak += band[i - 1];
                        }
                    }
                }
                valley = valley / neighbours + Tiny;
                peak = peak / neighbours + Tiny;

                contrast[b] = -Math.Pow(peak / valley, 1 / Math.Log(mean));
                valleys[b] = Math.Log(valley);
                start += size;
            }
        }
    }
}
