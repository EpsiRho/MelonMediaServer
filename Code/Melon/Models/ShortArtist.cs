using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortArtist
    {
        public ObjectId _id { get; set; }
        public string ArtistId { get; set; }
        public string ArtistName { get; set; }
    }
}
