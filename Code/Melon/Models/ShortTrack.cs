using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortTrack
    {
        public ObjectId _id { get; set; }
        public string AlbumName { get; set; }
        public uint Position { get; set; }
        public string TrackName { get; set; }
        public string Duration { get; set; }
        public string TrackArt { get; set; }
        public List<ShortArtist> TrackArtists { get; set; }

        public ShortTrack()
        {

        }
        public ShortTrack(Track t)
        {
            _id = t._id;
            AlbumName = t.AlbumName;
            Position = t.Position;
            TrackName = t.TrackName;
            Duration = t.Duration;
            TrackArtists = t.TrackArtists;
            TrackArt = t.TrackArt;
        }
    }
}
