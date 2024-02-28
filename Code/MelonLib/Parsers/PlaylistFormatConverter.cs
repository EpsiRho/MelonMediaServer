using Melon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonLib.Parsers
{
    public static class PlaylistFormatConverter
    {
        public static KeyValuePair<string, List<string>> FromM3U(string path)
        {
            string text = File.ReadAllText(path);
            string name = "";
            List<string> paths = new List<string>();

            if (!text.StartsWith("#EXTM3U"))
            {
                return KeyValuePair.Create(name, paths);
            }

            if (text.Contains("#PLAYLIST"))
            {
                name = path.Replace("\\", "/").Split("/").Last().Split(".")[0];
            }

            foreach(var line in text.Replace("\r","").Split("\n"))
            {
                if (!line.StartsWith("#") && line != "")
                {
                    paths.Add(line);
                }
            }

            return KeyValuePair.Create(name, paths);
        }
        public static KeyValuePair<string, List<string>> FromPLS(string path)
        {
            string text = File.ReadAllText(path);
            string name = path.Replace("\\", "/").Split("/").Last().Split(".")[0];
            List<string> paths = new List<string>();

            if (!text.StartsWith("[playlist]"))
            {
                return KeyValuePair.Create(name, paths);
            }

            foreach (var line in text.Replace("\r", "").Split("\n"))
            {
                if (line.StartsWith("File"))
                {
                    paths.Add(line.Split("=")[1]);
                }
            }

            return KeyValuePair.Create(name, paths);
        }
        public static KeyValuePair<string, List<string>> FromXML(string path)
        {
            string text = File.ReadAllText(path);
            string name = path.Replace("\\", "/").Split("/").Last().Split(".")[0];
            List<string> paths = new List<string>();

            if (!text.StartsWith("<?xml"))
            {
                return KeyValuePair.Create(name, paths);
            }

            text = text.Replace("\t", "");

            foreach (var line in text.Replace("\r", "").Split("\n"))
            {
                var l = line.Trim();
                if (l.StartsWith("<location>"))
                {
                    var loc = l.Split(">")[1].Split("</")[0];
                    loc = loc.Replace("file://", "");
                    paths.Add(loc);
                }
            }

            return KeyValuePair.Create(name, paths);
        }

        public static string ToM3U(string name, List<Track> tracks)
        {
            string txt = $"#EXTM3U\n\n#PLAYLIST:{name}\n\n";
            foreach(var track in tracks)
            {
                double dur = 0;
                try
                {
                    dur = Convert.ToDouble((track.Duration)) / 1000;
                }
                catch (Exception)
                {

                }

                string artistNames = String.Join(";", track.TrackArtists.Select(x=>x.Name));

                txt += $"#EXTINF:{dur},{artistNames} - {track.Name}\n";
                txt += $"{track.Path}\n\n";
            }

            return txt;
        }
        public static string ToPLS(List<Track> tracks)
        {
            string txt = "[playlist]\n\n";
            int count = 1;
            foreach(var track in tracks)
            {
                double dur = 0;
                try
                {
                    dur = Convert.ToDouble((track.Duration)) / 1000;
                }
                catch (Exception)
                {

                }

                string artistNames = String.Join(";", track.TrackArtists.Select(x=>x.Name));

                txt += $"File{count}={track.Path}\n";
                txt += $"Title{count}={track.Name}\n";
                txt += $"Length{count}={dur}\n\n";
                count++;
            }

            txt += $"NumberOfEntries={count}";
            return txt;
        }
        public static string ToXML(List<Track> tracks)
        {
            string txt = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<playlist version=\"1\" xmlns=\"http://xspf.org/ns/0/\">\n\t<trackList>\n";
            foreach(var track in tracks)
            {
                txt += $"\t\t<track>\n\t\t\t<title>{track.Name}</title>\n\t\t\t<location>file://{track.Path}</location>\n\t\t</track>\n";
            }

            txt += $"\t</tracklist>\n</playlist>";

            return txt;
        }
    }
}
