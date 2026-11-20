namespace Melon.Models
{
    public class Playlist
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long TrackCount { get; set; }
        public string Owner { get; set; }
        public List<string> Editors { get; set; }
        public List<string> Viewers { get; set; }
        public bool PublicViewing { get; set; }
        public bool PublicEditing { get; set; }
        /// <summary>The view the playlist/collection opens in: "tracks", "albums" or "artists" (null = tracks).</summary>
        public string DefaultView { get; set; }
        public string ArtworkPath { get; set; }
        public List<DbLink> Tracks { get; set; }
    }
    public class ResponsePlaylist
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long TrackCount { get; set; }
        public string Owner { get; set; }
        public List<string> Editors { get; set; }
        public List<string> Viewers { get; set; }
        public bool PublicViewing { get; set; }
        public bool PublicEditing { get; set; }
        /// <summary>The view the playlist/collection opens in: "tracks", "albums" or "artists" (null = tracks).</summary>
        public string DefaultView { get; set; }
    }


}
