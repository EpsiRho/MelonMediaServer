using MongoDB.Bson;

namespace Melon.Models
{
    public class User
    {
        public string _id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Bio { get; set; }
        public byte[] Salt { get; set; }
        public string Type { get; set; }
        public string FavTrack { get; set; }
        public string FavAlbum { get; set; }
        public string FavArtist { get; set; }
        public bool PublicStats { get; set; }
        public List<string> Friends { get; set; }
        public DateTime LastLogin { get; set; }

    }
}
