using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class Chapter
    {
        public string _id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TimeSpan Timestamp { get; set; }
        public List<DbLink> Tracks { get; set; }
        public List<DbLink> Albums { get; set; }
        public List<DbLink> Artists { get; set; }

    }
}
