using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    public class ScanProgress
    {
        public double ScannedFiles { get; set; }
        public double FoundFiles { get; set; }
        public double averageMillisecondsPerFile { get; set; }
        public double estimatedTimeLeft { get; set; }
    }
}
