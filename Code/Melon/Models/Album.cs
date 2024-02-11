using Melon.Models;
using MongoDB.Bson;

namespace Melon.Models
{
    public class Album
    {
        public string _id { get; set; }
        public int TotalDiscs { get; set; }
        public int TotalTracks { get; set; }
        public string Name { get; set; }
        public string Bio { get; set; }
        public string Publisher { get; set; }
        public string ReleaseStatus { get; set; }
        public string ReleaseType { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public string ServerURL { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int AlbumArtCount { get; set; }
        public int AlbumArtDefault { get; set; }
        public List<string> AlbumArtPaths { get; set; }
        public List<string> AlbumGenres { get; set; }
        public List<DbLink> AlbumArtists { get; set; }
        public List<DbLink> ContributingArtists { get; set; }
        public List<DbLink> Tracks { get; set; }
    }
    public class ResponseAlbum
    {
        public string _id { get; set; }
        public int TotalDiscs { get; set; }
        public int TotalTracks { get; set; }
        public string Name { get; set; }
        public string Bio { get; set; }
        public string Publisher { get; set; }
        public string ReleaseStatus { get; set; }
        public string ReleaseType { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public string ServerURL { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int AlbumArtCount { get; set; }
        public int AlbumArtDefault { get; set; }
        public List<string> AlbumGenres { get; set; }
        public List<DbLink> AlbumArtists { get; set; }
        public List<DbLink> ContributingArtists { get; set; }
    }
}



