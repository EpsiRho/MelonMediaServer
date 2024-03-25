using Melon.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Models
{
    /// <summary>
    /// All of Melon's Settings.
    /// </summary>
    public class Settings
    {
        public string? MongoDbConnectionString { get; set; }
        public List<string> LibraryPaths { get; set; }
        public string ListeningURL { get; set; }
        public string DefaultLanguage { get; set; }
        public int JWTExpireInMinutes { get; set; }
        public Color Text { get; set; }
        public Color ShadedText { get; set; }
        public Color BackgroundText { get; set; }
        public Color Highlight { get; set; }
        public Color Melon { get; set; }
        public Color Error { get; set; }
        public bool UseMenuColor { get; set; }
        public double QueueCleanupWaitInHours { get; set; }
    }
    public class ShortSettings
    {
        public string? MongoDbConnectionString { get; set; }
        public List<string> LibraryPaths { get; set; }
        public string ListeningURL { get; set; }
        public string DefaultLanguage { get; set; }
        public Color Text { get; set; }
        public Color ShadedText { get; set; }
        public Color BackgroundText { get; set; }
        public Color Highlight { get; set; }
        public Color Melon { get; set; }
        public Color Error { get; set; }
        public bool UseMenuColor { get; set; }
        public ShortSettings(Settings set)
        {
            MongoDbConnectionString = set.MongoDbConnectionString;
            LibraryPaths = set.LibraryPaths;
            ListeningURL = set.ListeningURL;
            DefaultLanguage = set.DefaultLanguage;
            Text = set.Text;
            ShadedText = set.ShadedText;
            BackgroundText = set.BackgroundText;
            Highlight = set.Highlight;
            Melon = set.Melon;
            Error = set.Error;
            UseMenuColor = set.UseMenuColor;
            UseMenuColor = set.UseMenuColor;
        }
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
        public bool DisablePlugins { get; set; }
    }
}
