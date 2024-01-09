using DnsClient;
using Melon.Models;
using MongoDB.Bson;

namespace Melon.Models
{
    public class Track
    {
        public ObjectId _id { get; set; }
        public string TrackId { get; set; }
        public ShortAlbum Album { get; set; }
        public int Position { get; set; }
        public int Disc { get; set; }
        public string Format { get; set; }
        public string Bitrate { get; set; }
        public string SampleRate { get; set; }
        public string Channels { get; set; }
        public string BitsPerSample { get; set; }
        public string MusicBrainzID { get; set; }
        public string ISRC { get; set; }
        public string Year { get; set; }
        public string TrackName { get; set; }
        public string Path { get; set; }
        public string Duration { get; set; }
        public long PlayCount { get; set; }
        public long SkipCount { get; set; }
        public float Rating { get; set; }
        public int TrackArtCount { get; set; }
        public string LyricsPath { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> TrackGenres { get; set; }
        public List<ShortArtist> TrackArtists { get; set; }
    }
}

