using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortAlbum
    {
        public MelonId _id { get; set; }
        public string AlbumId { get; set; }
        public string AlbumName { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string ReleaseType { get; set; }
        public List<ShortArtist> AlbumArtists { get; set; }
        public List<ShortArtist> ContributingArtists { get; set; }
        public ShortAlbum() 
        {
            
        }
        public ShortAlbum(Album a) 
        {
            _id = a._id;
            AlbumId = a.AlbumId;
            AlbumName = a.AlbumName;
            ReleaseDate = a.ReleaseDate;
            ReleaseType = a.ReleaseType;
            AlbumArtists = a.AlbumArtists;
            ContributingArtists = a.ContributingArtists;
        }
    }
}

