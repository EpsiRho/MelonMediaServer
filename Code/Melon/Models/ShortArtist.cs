using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortArtist
    {
        public string _id { get; set; }
        public string ArtistName { get; set; }
        public ShortArtist()
        {

        }
        public ShortArtist(Artist a)
        {
            _id = a._id;
            ArtistName = a.ArtistName;
        }
    }
}
