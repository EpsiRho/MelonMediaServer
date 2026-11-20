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
        public List<DbLink> ConnectedArtists { get; set; }
        /// <summary>Resolved by the artwork service when a provider needs it. Null until looked up, empty when no match.</summary>
        public string? MusicBrainzArtistId { get; set; }
        /// <summary>When online artwork providers were last asked about this artist.</summary>
        public DateTime? ArtworkCheckedUtc { get; set; }
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
            Genres = new List<string>();
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
            Genres = new List<string>();
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

