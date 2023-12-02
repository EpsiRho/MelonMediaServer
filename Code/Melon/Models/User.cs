using MongoDB.Bson;

namespace Melon.Models
{
    public class User
    {
        public ObjectId _id { get; set; }
        public string Name { get; set; }
        public DateTime LastLogin { get; set; }

    }
}
