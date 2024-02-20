using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Interface
{
    public interface IPlugin
    {
        public string Name { get; }
        public string Description { get; }
        public IHost Host { get; set; }
        public void LoadMelonCommands(IHost host);
        public int Execute();
    }
}
