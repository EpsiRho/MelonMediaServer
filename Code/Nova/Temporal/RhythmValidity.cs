namespace Nova.Temporal
{
    /// <summary>What the beat-based features can be trusted for.</summary>
    public enum RhythmStatus
    {
        /// <summary>No salient pulse: beatless, ambient, speech or silence. The grid, pattern and bars are arbitrary.</summary>
        NoPulse,

        /// <summary>There is a pulse, but the tracker's beats don't fit it well, or cover too little of the track.</summary>
        Uncertain,

        /// <summary>A pulse the beats fit, over most of the track. The meter is still assumed to be 4/4.</summary>
        Pulse,
    }

    /// <summary>
    /// How far to trust the beat grid, from evidence independent of it. Low pattern clarity alone can't say this: a
    /// groove that changes between sections also folds flat, and steady beats can come from the tracker's smoothing.
    /// <list type="bullet">
    /// <item>Pulse coverage: share of the track's audible seconds whose 8-second window has salience of at least
    /// <see cref="SalientPulse"/> (<see cref="Pulse"/>).</item>
    /// <item>Beat support: median, over those seconds, of how well the tracker's period fits the window.</item>
    /// <item>Bar coverage: share of the track inside complete bars; LeadIn and Tail are the seconds before the first
    /// bar and after the last, which bar-level features leave out.</item>
    /// </list>
    /// Status: <see cref="RhythmStatus.NoPulse"/> with fewer than 8 beats or pulse coverage under 0.25;
    /// <see cref="RhythmStatus.Uncertain"/> with pulse coverage under 0.6, beat support under 0.6 or bar coverage under
    /// 0.6; otherwise <see cref="RhythmStatus.Pulse"/>. <see cref="SalientPulse"/> is 0.25: pure noise reaches it on at most
    /// 2% of its seconds (its median salience is 0.14), so a track needs real periodicity to pass. The thresholds are
    /// still provisional (a look at a sample of the open
    /// corpus's training split, 2026-11-08), so the numbers they come from are kept and the status can be recomputed.
    /// </summary>
    /// <param name="MeterAssumed">Beats per bar the grid assumes; nothing estimates the meter yet.</param>
    public sealed record RhythmValidity(
        RhythmStatus Status,
        double PulseSalience,
        double PulseCoverage,
        double BeatSupport,
        double BeatCoverage,
        double BarCoverage,
        double LeadIn,
        double Tail,
        int MeterAssumed)
    {
        public const double SalientPulse = 0.25;
        public const double NoPulseCoverage = 0.25;
        public const double MinCoverage = 0.6;
        public const double MinSupport = 0.6;
        public const double MinBarCoverage = 0.6;
        private const int MinBeats = 8;
        private const double AudibleDb = -60;

        public static RhythmValidity Compute(double duration, BeatGrid grid, BeatSequence beats, ClockSeries clock)
        {
            var audible = Enumerable.Range(0, clock.Count).Where(r => clock.Loudness[r] > AudibleDb).ToArray();
            var salient = audible.Where(r => clock.PulseSalience[r] >= SalientPulse).ToArray();
            var salience = audible.Length > 0 ? TempoStability.Median(audible.Select(r => clock.PulseSalience[r]).ToArray()) : 0;
            var coverage = audible.Length > 0 ? (double)salient.Length / audible.Length : 0;
            var support = salient.Length > 0 ? TempoStability.Median(salient.Select(r => clock.BeatSupport[r]).ToArray()) : 0;

            double beatCoverage = 0, barCoverage = 0, leadIn = duration, tail = 0;
            if (beats.Count > 0 && duration > 0)
            {
                beatCoverage = Math.Min(1, (beats.Time[^1] + beats.Duration[^1] - beats.Time[0]) / duration);
            }
            if (grid.Bars > 0 && duration > 0)
            {
                var first = beats.Time[grid.BarStarts[0]];
                var lastBeat = grid.BarStarts[^1] + grid.BeatsPerBar - 1;
                var end = beats.Time[lastBeat] + beats.Duration[lastBeat];
                barCoverage = Math.Min(1, (end - first) / duration);
                leadIn = first;
                tail = Math.Max(0, duration - end);
            }

            RhythmStatus status;
            if (beats.Count < MinBeats || coverage < NoPulseCoverage)
            {
                status = RhythmStatus.NoPulse;
            }
            else if (coverage < MinCoverage || support < MinSupport || barCoverage < MinBarCoverage)
            {
                status = RhythmStatus.Uncertain;
            }
            else
            {
                status = RhythmStatus.Pulse;
            }
            return new RhythmValidity(status, salience, coverage, support, beatCoverage, barCoverage, leadIn, tail, grid.BeatsPerBar);
        }
    }
}
