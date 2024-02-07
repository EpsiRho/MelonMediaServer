using MongoDB.Bson;

namespace Melon.Models
{
    public class ShortAlbum
    {
        public string _id { get; set; }
        public string AlbumName { get; set; }
        public ShortAlbum() 
        {
            
        }
        public ShortAlbum(Album a) 
        {
            _id = a._id;
            AlbumName = a.AlbumName;
        }
    }
}

