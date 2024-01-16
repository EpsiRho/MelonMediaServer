using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortArtist
    {
        public MelonId _id { get; set; }
        public string ArtistId { get; set; }
        public string ArtistName { get; set; }
        public ShortArtist()
        {

        }
        public ShortArtist(Artist a)
        {
            _id = a._id;
            ArtistId = a.ArtistId;
            ArtistName = a.ArtistName;
        }
    }
}
