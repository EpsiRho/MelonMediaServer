using MongoDB.Bson;

namespace Melon.Models
{
    public class PlayQueue
    {
        public ObjectId _id { get; set; }
        public string QueueId { get; set; }
        public string Name { get; set; }
        public int CurPosition { get; set; }
        public List<ShortTrack> Tracks { get; set; }
    }
}
