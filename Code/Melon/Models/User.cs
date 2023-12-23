using MongoDB.Bson;

namespace Melon.Models
{
    public class User
    {
        public ObjectId _id { get; set; }
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Bio { get; set; }
        public byte[] Salt { get; set; }
        public string Type { get; set; }
        public bool PublicStats { get; set; }
        public DateTime LastLogin { get; set; }

    }
}
