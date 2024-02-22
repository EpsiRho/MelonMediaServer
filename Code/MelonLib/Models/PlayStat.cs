namespace Melon.Models
{
    public class PlayStat
    {
        public string _id { get; set; }
        public string TrackId { get; set; }
        public string AlbumId { get; set; }
        public List<string> ArtistIds { get; set; }
        public List<string> Genres { get; set; }
        public string Device { get; set; }
        public string UserId { get; set; }
        public string Duration { get; set; }
        public string Type { get; set; }
        public DateTime LogDate { get; set; }
    }
}