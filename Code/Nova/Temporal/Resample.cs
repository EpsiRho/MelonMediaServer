namespace Nova.Temporal
{
    /// <summary>
    /// Fixed-size views of curves and matrices whose length depends on the track: averaging over each output cell's
    /// span when shrinking (so no bar is skipped), linear interpolation when stretching.
    /// </summary>
    public static class Resample
    {
        public static double[] Linear(ReadOnlySpan<double> x, int size)
        {
            var output = new double[size];
            var n = x.Length;
            if (n == 0)
            {
                return output;
            }
            if (n == 1)
            {
                Array.Fill(output, x[0]);
                return output;
            }
            if (n >= size)
            {
                for (var i = 0; i < size; i++)
                {
                    var start = (double)i * n / size;
                    var end = (double)(i + 1) * n / size;
                    output[i] = Area(x, start, end);
                }
            }
            else
            {
                for (var i = 0; i < size; i++)
                {
                    var position = (double)i * (n - 1) / (size - 1);
                    var low = (int)Math.Floor(position);
                    var high = Math.Min(n - 1, low + 1);
                    output[i] = x[low] + (position - low) * (x[high] - x[low]);
                }
            }
            return output;
        }

        /// <summary>An n × n matrix (row-major) to size × size, rows first, then columns.</summary>
        public static double[] Matrix(double[] m, int n, int size)
        {
            var rows = new double[n * size];
            for (var r = 0; r < n; r++)
            {
                Linear(m.AsSpan(r * n, n), size).CopyTo(rows, r * size);
            }
            return Columns(rows, n, size);
        }

        /// <summary>The second half of <see cref="Matrix"/>: n rows already resized to size columns, resized to size rows.</summary>
        public static double[] Columns(double[] rows, int n, int size)
        {
            var output = new double[size * size];
            var column = new double[n];
            for (var c = 0; c < size; c++)
            {
                for (var r = 0; r < n; r++)
                {
                    column[r] = rows[r * size + c];
                }
                var resized = Linear(column, size);
                for (var r = 0; r < size; r++)
                {
                    output[r * size + c] = resized[r];
                }
            }
            return output;
        }

        /// <summary>Mean of x over the continuous span [start, end), each sample weighted by its overlap.</summary>
        private static double Area(ReadOnlySpan<double> x, double start, double end)
        {
            double sum = 0, weight = 0;
            for (var i = (int)Math.Floor(start); i < Math.Ceiling(end) && i < x.Length; i++)
            {
                var overlap = Math.Min(end, i + 1) - Math.Max(start, i);
                if (overlap > 0)
                {
                    sum += overlap * x[i];
                    weight += overlap;
                }
            }
            return weight > 0 ? sum / weight : 0;
        }
    }
}
