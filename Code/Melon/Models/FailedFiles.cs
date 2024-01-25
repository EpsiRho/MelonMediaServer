using MongoDB.Bson;

namespace Melon.Models
{
    public class FailedFiles
    {
        public MelonId _id { get; set; }
        public string Type { get; set; }
        public List<string> Paths { get; set; }
    }
}
