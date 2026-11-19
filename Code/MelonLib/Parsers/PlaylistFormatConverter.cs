using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Melon.Models;

namespace MelonLib.Parsers;

public static class PlaylistFormatConverter
{
    public static KeyValuePair<string, List<string>> FromM3U(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var tracks = new List<string>();
        foreach (var line in File.ReadLines(path).Select(line => line.Trim()))
        {
            if (line.StartsWith("#PLAYLIST:", StringComparison.OrdinalIgnoreCase) && line.Length > 10)
                name = line[10..];
            else if (line.Length > 0 && !line.StartsWith('#'))
                tracks.Add(LocalPath(line));
        }
        return new(name, tracks);
    }

    public static KeyValuePair<string, List<string>> FromPLS(string path)
    {
        var files = new SortedDictionary<int, string>();
        foreach (var line in File.ReadLines(path))
        {
            int equals = line.IndexOf('=');
            if (equals > 4 && line.StartsWith("File", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line.AsSpan(4, equals - 4), out int index) && equals + 1 < line.Length)
                files[index] = LocalPath(line[(equals + 1)..]);
        }
        return new(Path.GetFileNameWithoutExtension(path), [.. files.Values]);
    }

    public static KeyValuePair<string, List<string>> FromXML(string path)
    {
        var xml = File.ReadAllText(path);
        // Older Melon exports used a lowercase closing tag and unescaped text.
        if (xml.Contains("<trackList>") && xml.Contains("</tracklist>"))
        {
            var paths = xml.Split('\n').Select(line => line.Trim())
                .Where(line => line.StartsWith("<location>") && line.EndsWith("</location>"))
                .Select(line => LocalPath(line[10..^11])).ToList();
            return new(Path.GetFileNameWithoutExtension(path), paths);
        }
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var root = XElement.Load(reader);
        return new(Path.GetFileNameWithoutExtension(path), root.Descendants().Where(e => e.Name.LocalName == "location")
            .Select(e => LocalPath(e.Value)).ToList());
    }

    private static string LocalPath(string value)
    {
        // Original Windows exports used file://C:/... instead of file:///C:/...
        if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && value.Length > 9 && value[8] == ':')
            return value[7..];
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : value;
    }

    private static string Seconds(Track track) =>
        double.TryParse(track.Duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds)
            ? (milliseconds / 1000).ToString(CultureInfo.InvariantCulture) : "-1";

    public static string ToM3U(string name, List<Track> tracks)
    {
        var result = new StringBuilder().AppendLine("#EXTM3U").AppendLine("#PLAYLIST:" + name);
        foreach (var track in tracks)
        {
            var artists = string.Join(";", track.TrackArtists.Select(artist => artist.Name));
            result.AppendLine($"#EXTINF:{Seconds(track)},{artists} - {track.Name}").AppendLine(track.Path);
        }
        return result.ToString();
    }

    public static string ToPLS(List<Track> tracks)
    {
        var result = new StringBuilder().AppendLine("[playlist]");
        int i = 0;
        foreach (var track in tracks)
        {
            i++;
            result.AppendLine($"File{i}={track.Path}").AppendLine($"Title{i}={track.Name}").AppendLine($"Length{i}={Seconds(track)}");
        }
        return result.AppendLine($"NumberOfEntries={i}").AppendLine("Version=2").ToString();
    }

    public static string ToXML(List<Track> tracks)
    {
        XNamespace ns = "http://xspf.org/ns/0/";
        return new XElement(ns + "playlist", new XAttribute("version", 1),
            new XElement(ns + "trackList", tracks.Select(track => new XElement(ns + "track",
                new XElement(ns + "title", track.Name),
                new XElement(ns + "location", new Uri(Path.GetFullPath(track.Path)).AbsoluteUri))))).ToString();
    }
}
