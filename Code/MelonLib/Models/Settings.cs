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
        public List<string> ArtistSplitIndicators { get; set; }
        public List<string> GenreSplitIndicators { get; set; }
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
        public double QueueCleanupWaitInHours { get; set; } = 48;
        /// <summary>Largest artwork upload accepted, in megabytes. Local controllers only.</summary>
        public int MaxArtUploadMegabytes { get; set; } = 20;
        /// <summary>Largest playlist/collection import accepted, in megabytes. Local controllers only.</summary>
        public int MaxPlaylistImportMegabytes { get; set; } = 2;
        /// <summary>Size at which the server log starts a new file, in megabytes. Local controllers only.</summary>
        public int LogFileMegabytes { get; set; } = 100;
        /// <summary>Log files kept, counting the current one; older ones are deleted. Local controllers only.</summary>
        public int LogFilesKept { get; set; } = 5;
        /// <summary>
        /// "Automatic": Melon listens on <see cref="HttpPort"/> and <see cref="HttpsPort"/> with its own certificate, broadcasts on the
        /// local network and refuses outside addresses unless Remote Access is on. "Custom": <see cref="ListeningURL"/> and the
        /// configured certificate, as set up by hand. Settings from before this option load as Custom so nothing changes for them.
        /// </summary>
        public string? NetworkMode { get; set; }
        public int HttpPort { get; set; } = 14524;
        public int HttpsPort { get; set; } = 14525;
        /// <summary>Advertise Melon on the local network (mDNS, _melon._tcp).</summary>
        public bool Broadcast { get; set; } = true;
        /// <summary>Instance name other devices see. Empty means "Melon on {computer}".</summary>
        public string BroadcastName { get; set; } = "";
        public RemoteAccessSettings RemoteAccess { get; set; } = new();
        /// <summary>Addresses or CIDR ranges refused before any other handling.</summary>
        public List<BlockedAddress> BlockedAddresses { get; set; } = [];
        /// <summary>Public IPv4 addresses seen over time, newest last, to tell whether the address changes.</summary>
        public List<string> PublicAddressHistory { get; set; } = [];
    }
    public class RemoteAccessSettings
    {
        public bool Enabled { get; set; }
        /// <summary>Automatic, Tailscale or ReverseProxy.</summary>
        public string Method { get; set; } = "Automatic";
        public bool OpenRouterPort { get; set; } = true;
        /// <summary>Port opened on the router; 0 means the same as HttpsPort.</summary>
        public int ExternalPort { get; set; }
        public bool UseIPv6 { get; set; }
        public bool PublishPkarr { get; set; } = true;
        /// <summary>ReverseProxy: the address clients use.</summary>
        public string? PublicUrl { get; set; }
        /// <summary>Pkarr relays to publish through. Empty means <see cref="DefaultPkarrRelays"/>. (Not pre-filled: Newtonsoft appends to lists.)</summary>
        public List<string> PkarrRelays { get; set; } = [];
        public static readonly string[] DefaultPkarrRelays = ["https://relay.pkarr.org", "https://pkarr.pubky.app"];
    }
    public class BlockedAddress
    {
        public string Address { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime BlockedUtc { get; set; }
    }
    public class ShortSettings
    {
        public string? MongoDbConnectionString { get; set; }
        public List<string> LibraryPaths { get; set; }
        public List<string> ArtistSplitIndicators { get; set; }
        public List<string> GenreSplitIndicators { get; set; }
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
            LibraryPaths = [.. set.LibraryPaths];
            ArtistSplitIndicators = [.. set.ArtistSplitIndicators];
            GenreSplitIndicators = [.. set.GenreSplitIndicators];
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
