using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class MelonId
    {
        public int timestamp { get; set; }
        public int machine { get; set; }
        public short pid { get; set; }
        public int increment { get; set; }
        public DateTime creationTime { get; set; }
        public MelonId(ObjectId id)
        {
            timestamp = id.Timestamp;
            machine = id.Machine;
            pid = id.Pid;
            increment = id.Increment;
            creationTime = id.CreationTime;
        }
        public override string ToString()
        {
            var id = new ObjectId(creationTime, machine, pid, increment);
            return id.ToString();
        }
    }
}
