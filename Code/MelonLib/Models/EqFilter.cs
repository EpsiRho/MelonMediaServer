#nullable disable
namespace Melon.Models
{
    /// <summary>
    /// A saved equalizer. Owned by one user; visible to everyone when <see cref="IsPublic"/> is on. Pass its id as
    /// <c>eq</c> on api/download/track or track-transcode to have the server apply it. Processed by Nova's
    /// <c>Nova.Dsp.Equalizer</c>, whose limits apply (32 bands, ±30 dB, 10 Hz–40 kHz, Q 0.05–40).
    /// </summary>
    public class EqFilter
    {
        public string _id { get; set; }
        public string Name { get; set; }
        /// <summary>User id of the owner.</summary>
        public string Owner { get; set; }
        public bool IsPublic { get; set; }
        /// <summary>Output gain in dB, applied after the bands.</summary>
        public double Preamp { get; set; }
        /// <summary>Lower the output by the curve's highest boost so boosts don't clip (default on).</summary>
        public bool AutoHeadroom { get; set; } = true;
        public List<EqFilterBand> Bands { get; set; } = new();
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }

    public class EqFilterBand
    {
        /// <summary>peak, lowShelf, highShelf, lowPass, highPass, bandPass or notch (case-insensitive).</summary>
        public string Type { get; set; } = "peak";
        /// <summary>Centre or corner frequency in Hz.</summary>
        public double Frequency { get; set; } = 1000;
        /// <summary>Boost/cut in dB (peak and shelves).</summary>
        public double Gain { get; set; }
        /// <summary>Width, higher is narrower (1.41 ≈ one octave for a peak; 0.707 for smooth shelves and cuts).</summary>
        public double Q { get; set; } = 0.7071067811865476;
        /// <summary>lowPass/highPass roll-off in dB/octave: 6, 12, 18, 24, 36 or 48.</summary>
        public int Slope { get; set; } = 12;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Body for api/eq/add and api/eq/update.</summary>
    public class EqFilterRequest
    {
        public string Name { get; set; }
        /// <summary>Only read by add; use api/eq/set-public to change it later. Null keeps the current value.</summary>
        public bool? IsPublic { get; set; }
        public double Preamp { get; set; }
        public bool AutoHeadroom { get; set; } = true;
        public List<EqFilterBand> Bands { get; set; } = new();
    }

    /// <summary>An EQ filter as clients see it, with the owner's name for lists.</summary>
    public class ResponseEqFilter
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public string OwnerName { get; set; }
        public bool IsPublic { get; set; }
        public double Preamp { get; set; }
        public bool AutoHeadroom { get; set; }
        public List<EqFilterBand> Bands { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }

        public ResponseEqFilter() { }
        public ResponseEqFilter(EqFilter f, string ownerName)
        {
            _id = f._id;
            Name = f.Name;
            Owner = f.Owner;
            OwnerName = ownerName;
            IsPublic = f.IsPublic;
            Preamp = f.Preamp;
            AutoHeadroom = f.AutoHeadroom;
            Bands = f.Bands;
            Created = f.Created;
            Updated = f.Updated;
        }
    }

    /// <summary>A filter's response curve for drawing: gain in dB (bands only) at each frequency, plus the output gain.</summary>
    public class EqResponse
    {
        public double[] Frequencies { get; set; }
        public double[] Gains { get; set; }
        /// <summary>Preamp minus automatic headroom, in dB.</summary>
        public double OutputGain { get; set; }
        public int SampleRate { get; set; }
    }
}
