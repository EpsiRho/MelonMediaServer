using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class PlayStat
    {
        public MelonId _id { get; set; }
        public string StatId { get; set; }
        public string TrackId { get; set; }
        public string AlbumId { get; set; }
        public List<string> ArtistIds { get; set; }
        public List<string> Genres { get; set; }
        public string Device { get; set; }
        public string User { get; set; }
        public string Duration { get; set; }
        public DateTime LogDate { get; set; }
    }
}