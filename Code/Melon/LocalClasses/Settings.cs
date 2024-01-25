using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
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
        public byte[]? JWTKey { get; set; }
        public List<string> LibraryPaths { get; set; }
        public string ListeningURL { get; set; }
        public int JWTExpireInMinutes { get; set; }
        public Color Text { get; set; }
        public Color ShadedText { get; set; }
        public Color BackgroundText { get; set; }
        public Color Highlight { get; set; }
        public Color Melon { get; set; }
        public Color Error { get; set; }
        public bool UseMenuColor { get; set; }
    }
    /// <summary>
    /// Configuration for SSL
    /// </summary>
    public class SSLConfig
    {
        public string? PathToCert { get; set; }
        public string? Password { get; set; }
    }
    public class Connection
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? URL { get; set; }
        public string? JWT { get; set; }
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
