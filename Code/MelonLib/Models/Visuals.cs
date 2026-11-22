namespace Melon.Models;

/// <summary>
/// Beat and hit times for a track (GET api/visuals/beat-times). Arrays are columnar: index i of every array in
/// <see cref="Beats"/> describes beat i, likewise for <see cref="Hits"/>. Times are seconds from the start of the file.
/// </summary>
/// <param name="Rhythm">"pulse" (steady beat), "uncertain" (beats don't fit well) or "noPulse" (beatless: ambient, speech).
/// Treat beats as decorative unless this is "pulse".</param>
/// <param name="DownbeatConfidence">0–1. How sure the bar grid is about which beat is beat 1 (meter is assumed 4/4).</param>
public record BeatTimes(int Version, string TrackId, double Duration, double Bpm, string Rhythm, double DownbeatConfidence,
    int BeatsPerBar, BeatList Beats, HitList Hits);

/// <param name="Time">Beat positions from the beat tracker (the grid you'd tap along to).</param>
/// <param name="Downbeat">True on the first beat of each bar.</param>
/// <param name="Strength">0–1 onset strength on the beat across all bands, relative to the track's loud beats.
/// Use it as haptic intensity or pulse size.</param>
/// <param name="Sharpness">0–1 share of the beat's onset energy above 800 Hz: 0 is a deep kick or bass thud, 1 a crisp
/// snare, clap or hat. Maps directly onto Core Haptics sharpness.</param>
/// <param name="Low">Onset strength under 150 Hz (kick, bass), 0–1.</param>
/// <param name="LowMid">150–800 Hz (snare body, toms), 0–1.</param>
/// <param name="HighMid">800–4,000 Hz (snare crack, claps, vocals), 0–1.</param>
/// <param name="High">Above 4,000 Hz (hats, cymbals), 0–1.</param>
public record BeatList(double[] Time, bool[] Downbeat, float[] Strength, float[] Sharpness,
    float[] Low, float[] LowMid, float[] HighMid, float[] High);

/// <summary>
/// Individual percussive hits found in the audio, independent of the beat grid, for effects that should land on the
/// actual drum hits (off-beat kicks, fills, hat patterns).
/// </summary>
/// <param name="Kind">"kick" (under 150 Hz), "snare" (150–4,000 Hz) or "hat" (above 4,000 Hz).</param>
/// <param name="Strength">0–1 relative to the track's strongest hits of the same kind.</param>
public record HitList(double[] Time, string[] Kind, float[] Strength);

/// <summary>
/// The track's shape over time (GET api/visuals/moments): an energy curve and labelled stretches to react to.
/// </summary>
/// <param name="Hop">Seconds per value in <see cref="Energy"/> and <see cref="Pulse"/> (1).</param>
/// <param name="Energy">0–1 per second, loudness and onset activity relative to this track, lightly smoothed.</param>
/// <param name="Pulse">0–1 per second, how clearly periodic the rhythm is around that second (0 beatless, 1 drum machine).</param>
/// <param name="Items">Labelled moments, sorted by start. They may overlap (a soft stretch can also be beatless).</param>
/// <param name="Sections">Structural boundaries found by comparing bars (verse, chorus, bridge), in order.</param>
public record Moments(int Version, string TrackId, double Duration, double Hop, float[] Energy, float[] Pulse,
    Moment[] Items, MomentSection[] Sections);

/// <param name="Kind">
/// "soft" (quiet: piano, a vocal alone, a slow pad), "energetic" (loud and busy), "peak" (the most intense energetic
/// stretch), "build" (energy rising into something), "drop" (a sudden jump in energy and bass; Start == End),
/// "beatless" (no clear pulse), or "silence".
/// </param>
/// <param name="Intensity">0–1. Mean energy for energetic, peak and build; quietness for soft; size of the jump for drop.</param>
public record Moment(string Kind, double Start, double End, float Intensity);

public record MomentSection(double Start, double End);

/// <summary>Body of POST api/visuals/prefetch.</summary>
/// <param name="Ids">Track ids, typically the next few in the queue (at most 8).</param>
/// <param name="Include">Any of "fvz", "beat-times", "moments". Empty means all three.</param>
/// <param name="Format">FFTVIS format to prepare; null fields use the server default.</param>
public record VisualPrefetchRequest(string[] Ids, string[]? Include = null, VisualFormatRequest? Format = null);

/// <summary>A client's requested FFTVIS format. Null fields use the server default; values are clamped to the limits.</summary>
public record VisualFormatRequest(int? Bands = null, int? Fps = null, int? FftResolution = null, string? Mapping = null,
    double? DbFloor = null, double? DbRange = null, double? FrequencyMin = null, double? FrequencyMax = null,
    int? Smoothness = null, bool? Stereo = null, int? QuantizeBits = null, double? DecayPerSecond = null, string? Codec = null);
