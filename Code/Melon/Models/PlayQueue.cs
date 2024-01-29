using MongoDB.Bson;

namespace Melon.Models
{
    public class PlayQueue
    {
        public MelonId _id { get; set; }
        public string QueueId { get; set; }
        public string Name { get; set; }
        public int CurPosition { get; set; }
        public string Owner { get; set; }
        public List<string> Editors { get; set; }
        public List<string> Viewers { get; set; }
        public bool PublicViewing { get; set; }
        public bool PublicEditing { get; set; }
        public List<Track> Tracks { get; set; }
        public List<string> OriginalTrackOrder { get; set; }
    }
}
