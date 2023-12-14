using MongoDB.Bson;

namespace Melon.Models
{
    public class Album
    {
        public ObjectId _id { get; set; }
        public string AlbumId { get; set; }
        public int TotalDiscs { get; set; }
        public int TotalTracks { get; set; }
        public string AlbumName { get; set; }
        public string Bio { get; set; }
        public string Publisher { get; set; }
        public string ReleaseStatus { get; set; }
        public string ReleaseType { get; set; }
        public long PlayCount { get; set; }
        public float Rating { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> AlbumArtPaths { get; set; }
        public List<string> AlbumGenres { get; set; }
        public List<ShortArtist> AlbumArtists { get; set; }
        public List<ShortTrack> Tracks { get; set; }
    }
}
