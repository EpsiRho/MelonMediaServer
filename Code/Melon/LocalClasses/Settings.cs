using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.LocalClasses
{
    /// <summary>
    /// All of Melon's Settings.
    /// New settings can be added here and will converted into json for saving to a file.
    /// </summary>
    public class Settings
    {
        public string? MongoDbConnectionString { get; set; }
        public string? JWTKey { get; set; }
        public List<string> LibraryPaths { get; set; }
        public string ListeningURL { get; set; }
        public Color Text { get; set; }
        public Color ShadedText { get; set; }
        public Color BackgroundText { get; set; }
        public Color Highlight { get; set; }
        public Color Melon { get; set; }
        public Color Error { get; set; }
        public bool UseMenuColor { get; set; }
    }
    /// <summary>
    /// Melon's debug flags. These are used to force enable/disable features for testing.
    /// They can be adjusted from the flags.json file in the Melon directory
    /// </summary>
    public class Flags
    {
        public bool ForceOOBE { get; set; }
    }
}
