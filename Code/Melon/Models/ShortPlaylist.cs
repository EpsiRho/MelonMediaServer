using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortPlaylist
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
        public string ArtworkPath { get; set; }
        public ShortPlaylist(Playlist p)
        {
            _id = p._id;
            Name = p.Name;
            Description = p.Description;
            TrackCount = p.TrackCount;
            ArtworkPath = p.ArtworkPath;
            Owner = p.Owner;
            Editors = p.Editors;
            Viewers = p.Viewers;
            PublicEditing = p.PublicEditing;
            PublicViewing = p.PublicViewing;
        }
    }

    
}
