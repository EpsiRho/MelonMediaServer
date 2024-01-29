using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortTrack
    {
        public MelonId _id { get; set; }
        public string TrackId { get; set; }
        public string TrackName { get; set; }

        public ShortTrack()
        {

        }
        public ShortTrack(Track t)
        {
            _id = t._id;
            TrackId = t.TrackId;
            TrackName = t.TrackName;
        }
    }
}
