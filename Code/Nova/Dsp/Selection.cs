namespace Nova.Dsp
{
    public static class Selection
    {
        /// <summary>
        /// Rearranges x so that x[k] is the value a full sort would put there, with nothing larger before it and
        /// nothing smaller after it (Hoare's quickselect with median-of-three pivots). Returns x[k].
        /// </summary>
        public static double Select(Span<double> x, int k)
        {
            int lo = 0, hi = x.Length - 1;
            while (hi > lo)
            {
                var mid = lo + (hi - lo) / 2;
                if (x[mid] < x[lo])
                {
                    (x[mid], x[lo]) = (x[lo], x[mid]);
                }
                if (x[hi] < x[lo])
                {
                    (x[hi], x[lo]) = (x[lo], x[hi]);
                }
                if (x[hi] < x[mid])
                {
                    (x[hi], x[mid]) = (x[mid], x[hi]);
                }
                var pivot = x[mid];

                int i = lo, j = hi;
                while (i <= j)
                {
                    while (x[i] < pivot)
                    {
                        i++;
                    }
                    while (x[j] > pivot)
                    {
                        j--;
                    }
                    if (i <= j)
                    {
                        (x[i], x[j]) = (x[j], x[i]);
                        i++;
                        j--;
                    }
                }

                if (k <= j)
                {
                    hi = j;
                }
                else if (k >= i)
                {
                    lo = i;
                }
                else
                {
                    break;
                }
            }
            return x[k];
        }

        /// <summary>Median (the mean of the two middle values for an even count); rearranges x.</summary>
        public static double Median(Span<double> x)
        {
            if (x.Length == 0)
            {
                return 0;
            }
            var mid = x.Length / 2;
            var upper = Select(x, mid);
            if (x.Length % 2 == 1)
            {
                return upper;
            }
            // After selection everything before mid is at most x[mid]; the largest of them is the lower middle.
            var lower = double.NegativeInfinity;
            foreach (var v in x[..mid])
            {
                lower = Math.Max(lower, v);
            }
            return 0.5 * (lower + upper);
        }
    }
}
