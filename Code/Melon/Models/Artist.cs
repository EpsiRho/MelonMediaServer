using Melon.Models;
using MongoDB.Bson;

namespace Melon.Models
{
    public class Artist
    {
        public string _id { get; set; }
        public string ArtistName { get; set; }
        public string Bio { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public string ServerURL { get; set; }
        public List<string> ArtistArtPaths { get; set; }
        public List<string> ArtistBannerPaths { get; set; }
        public List<string> Genres { get; set; }
        public DateTime DateAdded { get; set; }
        public List<ShortAlbum> Releases { get; set; }
        public List<ShortAlbum> SeenOn { get; set; }
        public List<ShortArtist> ConnectedArtists { get; set; }
        public List<ShortTrack> Tracks { get; set; }
    }
}

