using MongoDB.Bson;

namespace Melon.Models
{
    public class Playlist
    {
        public ObjectId _id { get; set; }
        public string PlaylistId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ArtworkPath { get; set; }
        public List<ShortTrack> Tracks { get; set; }

    }

    
}
