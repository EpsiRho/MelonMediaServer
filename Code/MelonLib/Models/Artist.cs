using Melon.Models;
using System.Xml.Linq;

namespace Melon.Models
{
    public class Artist
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Bio { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public string ServerURL { get; set; }
        public int ArtistPfpArtCount { get; set; }
        public int ArtistBannerArtCount { get; set; }
        public int ArtistPfpDefault { get; set; }
        public int ArtistBannerArtDefault { get; set; }
        public List<string> ArtistPfpPaths { get; set; }
        public List<string> ArtistBannerPaths { get; set; }
        public List<string> Genres { get; set; }
        public DateTime DateAdded { get; set; }
        public List<DbLink> Releases { get; set; }
        public List<DbLink> SeenOn { get; set; }
        public List<DbLink> ConnectedArtists { get; set; }
        public List<DbLink> Tracks { get; set; }
        public Artist()
        {

        }
        public Artist(string name)
        {
            _id = "";
            Name = name;
            Bio = "";
            Ratings = new List<UserStat>();
            DateAdded = DateTime.Now.ToUniversalTime();
            Releases = new List<DbLink>();
            Genres = new List<string>();
            SeenOn = new List<DbLink>();
            Tracks = new List<DbLink>();
            ConnectedArtists = new List<DbLink>();
            ArtistBannerArtCount = 0;
            ArtistPfpArtCount = 0;
            ArtistBannerArtDefault = 0;
            ArtistPfpDefault = 0;
            ArtistBannerPaths = new List<string>();
            ArtistPfpPaths = new List<string>();
            PlayCounts = new List<UserStat>();
            SkipCounts = new List<UserStat>();
            ServerURL = "";
        }
        public Artist(string name, string id)
        {
            _id = id;
            Name = name;
            Bio = "";
            Ratings = new List<UserStat>();
            DateAdded = DateTime.Now.ToUniversalTime();
            Releases = new List<DbLink>();
            Genres = new List<string>();
            SeenOn = new List<DbLink>();
            Tracks = new List<DbLink>();
            ConnectedArtists = new List<DbLink>();
            ArtistBannerArtCount = 0;
            ArtistPfpArtCount = 0;
            ArtistBannerArtDefault = 0;
            ArtistPfpDefault = 0;
            ArtistBannerPaths = new List<string>();
            ArtistPfpPaths = new List<string>();
            PlayCounts = new List<UserStat>();
            SkipCounts = new List<UserStat>();
            ServerURL = "";
        }
    }
    public class ResponseArtist
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Bio { get; set; }
        public int ArtistPfpArtCount { get; set; }
        public int ArtistBannerArtCount { get; set; }
        public int ArtistPfpDefault { get; set; }
        public int ArtistBannerArtDefault { get; set; }
        public List<UserStat> PlayCounts { get; set; }
        public List<UserStat> SkipCounts { get; set; }
        public List<UserStat> Ratings { get; set; }
        public string ServerURL { get; set; }
        public List<string> Genres { get; set; }
        public DateTime DateAdded { get; set; }
    }
}

