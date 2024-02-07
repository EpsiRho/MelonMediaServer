using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortQueue
    {
        public string _id { get; set; }
        public int CurPosition { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public List<string> Editors { get; set; }
        public List<string> Viewers { get; set; }
        public bool PublicViewing { get; set; }
        public bool PublicEditing { get; set; }
        public ShortQueue(PlayQueue q)
        {
            _id = q._id;
            CurPosition = q.CurPosition;
            Name = q.Name;
            Owner = q.Owner;
            Editors = q.Editors;
            Viewers = q.Viewers;
            PublicEditing = q.PublicEditing;
            PublicViewing = q.PublicViewing;
        }
    }
}
