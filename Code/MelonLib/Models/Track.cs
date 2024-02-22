using Melon.Models;

namespace Melon.Models
{
    public class Track
    {
        public string _id { get; set; }
        public DbLink Album { get; set; }
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
        public string Name { get; set; }
        public string Path { get; set; }
        public string Duration { get; set; }
        public string nextTrack { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public int TrackArtCount { get; set; }
        public int TrackArtDefault { get; set; }
        public string LyricsPath { get; set; }
        public string ServerURL { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> TrackGenres { get; set; }
        public List<DbLink> TrackArtists { get; set; }
    }
    public class ResponseTrack
    {
        public string _id { get; set; }
        public DbLink Album { get; set; }
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
        public string Name { get; set; }
        public string Duration { get; set; }
        public string nextTrack { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public int TrackArtCount { get; set; }
        public int TrackArtDefault { get; set; }
        public string ServerURL { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> TrackGenres { get; set; }
        public List<DbLink> TrackArtists { get; set; }
        public ResponseTrack(Track t)
        {
            _id = t._id;
            Album = t.Album;
            Position = t.Position;
            Disc = t.Disc;
            Format = t.Format;
            Bitrate = t.Bitrate;
            SampleRate = t.SampleRate;
            Channels = t.Channels;
            BitsPerSample = t.BitsPerSample;
            MusicBrainzID = t.MusicBrainzID;
            ISRC = t.ISRC;
            Year = t.Year;
            Name = t.Name;
            Duration = t.Duration;
            nextTrack = t.nextTrack;
            PlayCounts = t.PlayCounts;
            SkipCounts = t.SkipCounts;
            Ratings = t.Ratings;
            TrackArtCount = t.TrackArtCount;
            ServerURL = t.ServerURL;
            LastModified = t.LastModified;
            DateAdded = t.DateAdded;
            ReleaseDate = t.ReleaseDate;
            TrackGenres = t.TrackGenres;
            TrackArtists = t.TrackArtists;
        }
    }
}

