namespace Nova.Temporal
{
    /// <summary>
    /// A frame-wise descriptor in time order: frame t holds Values[t·Dims .. (t+1)·Dims) and is centred at t · HopSeconds,
    /// which is where Nova's centred frames sit (see Framing.Starts with startFromZero false).
    /// </summary>
    public sealed record FrameSeries(double[] Values, int Dims, double HopSeconds)
    {
        public int Count => Dims > 0 ? Values.Length / Dims : 0;

        public ReadOnlySpan<double> Frame(int t) => Values.AsSpan(t * Dims, Dims);

        /// <summary>
        /// Frames whose centres fall in [start, end). An interval shorter than a hop still gets the frame nearest its
        /// middle, so every beat has a value.
        /// </summary>
        public (int First, int End) Range(double start, double end)
        {
            if (Count == 0)
            {
                return (0, 0);
            }
            var first = Math.Max(0, (int)Math.Ceiling(start / HopSeconds - 1e-9));
            var stop = Math.Min(Count, (int)Math.Ceiling(end / HopSeconds - 1e-9));
            if (stop <= first)
            {
                var nearest = Math.Clamp((int)Math.Round((start + end) / 2 / HopSeconds), 0, Count - 1);
                return (nearest, nearest + 1);
            }
            return (first, stop);
        }

        /// <summary>Mean of the frames in [start, end), per dimension.</summary>
        public void Mean(double start, double end, Span<double> destination)
        {
            destination.Clear();
            var (first, stop) = Range(start, end);
            if (stop <= first)
            {
                return;
            }
            for (var t = first; t < stop; t++)
            {
                var frame = Frame(t);
                for (var d = 0; d < Dims; d++)
                {
                    destination[d] += frame[d];
                }
            }
            for (var d = 0; d < Dims; d++)
            {
                destination[d] /= stop - first;
            }
        }
    }
}
