using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortTrack
    {
        public ObjectId _id { get; set; }
        public string TrackId { get; set; }
        public ShortAlbum Album { get; set; }
        public int Position { get; set; }
        public int Disc { get; set; }
        public string TrackName { get; set; }
        public string Duration { get; set; }
        public int TrackArtCount { get; set; }
        public string Path { get; set; }
        public List<ShortArtist> TrackArtists { get; set; }

        public ShortTrack()
        {

        }
        public ShortTrack(Track t)
        {
            _id = t._id;
            Album = t.Album;
            Position = t.Position;
            TrackName = t.TrackName;
            Duration = t.Duration;
            TrackArtists = t.TrackArtists;
            TrackArtCount = t.TrackArtCount;
        }
    }
}
