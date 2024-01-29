using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortAlbum
    {
        public MelonId _id { get; set; }
        public string AlbumId { get; set; }
        public string AlbumName { get; set; }
        public ShortAlbum() 
        {
            
        }
        public ShortAlbum(Album a) 
        {
            _id = a._id;
            AlbumId = a.AlbumId;
            AlbumName = a.AlbumName;
        }
    }
}

