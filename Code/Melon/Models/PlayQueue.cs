using MongoDB.Bson;

namespace Melon.Models
{
    public class PlayQueue
    {
        public ObjectId _id { get; set; }
        public string Name { get; set; }
        public List<Track> Tracks { get; set; }
    }
}
