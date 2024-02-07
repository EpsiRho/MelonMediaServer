using MongoDB.Bson;

namespace Melon.Models
{
    public class FailedFile
    {
        public string _id { get; set; }
        public string Path { get; set; }
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
    }
}
