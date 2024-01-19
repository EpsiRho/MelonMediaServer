using Melon.Models;
using MongoDB.Bson;

namespace Melon.Models
{
    public class Artist
    {
        public MelonId _id { get; set; }
        public string ArtistId { get; set; }
        public string ArtistName { get; set; }
        public string Bio { get; set; }
        public long PlayCount { get; set; }
        public float Rating { get; set; }
        public string ServerURL { get; set; }
        public List<string> ArtistArtPaths { get; set; }
        public List<string> ArtistBannerPaths { get; set; }
        public List<string> Genres { get; set; }
        public DateTime DateAdded { get; set; }
        public List<ShortAlbum> Releases { get; set; }
        public List<ShortAlbum> SeenOn { get; set; }
        public List<ShortTrack> Tracks { get; set; }
    }
}

