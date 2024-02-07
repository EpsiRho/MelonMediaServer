using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortTrack
    {
        public string _id { get; set; }
        public string TrackName { get; set; }

        public ShortTrack()
        {

        }
        public ShortTrack(Track t)
        {
            _id = t._id;
            TrackName = t.TrackName;
        }
    }
}
