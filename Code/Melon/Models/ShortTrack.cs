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
            TrackId = t.TrackId;
            Album = t.Album;
            Position = t.Position;
            Disc = t.Disc;
            TrackName = t.TrackName;
            Duration = t.Duration;
            TrackArtCount = t.TrackArtCount;
            Path = t.Path;
            TrackArtists = t.TrackArtists;
        }
    }
}
