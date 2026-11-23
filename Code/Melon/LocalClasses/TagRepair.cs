using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace Melon.LocalClasses
{
    /// <summary>
    /// Best-effort track / disc numbers for files whose tags ATL can't fully read.
    /// </summary>
    /// <remarks>
    /// ATL reports a missing number as 0, not null, so <c>?? 1</c> style defaults never fire. Some files also
    /// carry a malformed atom (for example an empty <c>cprt</c> copyright box) that makes ATL stop reading the
    /// MP4 tag list early, losing every tag after it, including <c>trkn</c> and <c>disk</c>.
    /// When a number comes back as 0 this tries, in order: reading the MP4 atoms directly, the file name
    /// ("04. Title", "1-04 Title"), and the folder name ("CD2", "Disc 2"). Discs default to 1.
    /// </remarks>
    public static class TagRepair
    {
        public readonly record struct Numbers(int Track, int TrackTotal, int Disc, int DiscTotal);

        private static readonly string[] Mp4Extensions = { ".m4a", ".mp4", ".m4b", ".m4p", ".alac", ".aac" };
        private static readonly Regex FileNumber = new(@"^\s*(?:(?<disc>\d{1,2})\s*[-.]\s*)?(?<track>\d{1,3})(?=\D|$)", RegexOptions.Compiled);
        private static readonly Regex FolderDisc = new(@"(?i)\b(?:cd|disc|disk)\s*(?<disc>\d{1,2})\b", RegexOptions.Compiled);

        public static Numbers Resolve(ATL.Track meta, string path)
        {
            int track = meta.TrackNumber ?? 0, trackTotal = meta.TrackTotal ?? 0;
            int disc = meta.DiscNumber ?? 0, discTotal = meta.DiscTotal ?? 0;

            if (track <= 0 || disc <= 0)
            {
                // 1. The tags are usually still there, ATL just didn't reach them.
                if (Mp4Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(path);
                        if (track <= 0 && TryReadPair(bytes, "trkn"u8, out var t, out var tt)) { track = t; if (trackTotal <= 0) trackTotal = tt; }
                        if (disc <= 0 && TryReadPair(bytes, "disk"u8, out var d, out var dt)) { disc = d; if (discTotal <= 0) discTotal = dt; }
                    }
                    catch (Exception) { /* unreadable: fall through to names */ }
                }

                // 2. File name: "04. Title", "04 - Title", "1-04 Title".
                if (track <= 0 || disc <= 0)
                {
                    var m = FileNumber.Match(Path.GetFileNameWithoutExtension(path));
                    if (m.Success)
                    {
                        if (track <= 0 && int.TryParse(m.Groups["track"].Value, out var t) && t > 0) track = t;
                        if (disc <= 0 && m.Groups["disc"].Success && int.TryParse(m.Groups["disc"].Value, out var d) && d > 0) disc = d;
                    }
                }

                // 3. Folder name: ".../Album/CD2/01 Title.flac".
                if (disc <= 0)
                {
                    var folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
                    var m = FolderDisc.Match(folder ?? "");
                    if (m.Success && int.TryParse(m.Groups["disc"].Value, out var d) && d > 0) disc = d;
                }
            }

            if (track < 0) track = 0;
            if (disc <= 0) disc = 1;
            if (discTotal < disc) discTotal = disc;
            if (trackTotal < 0) trackTotal = 0;
            return new Numbers(track, trackTotal, disc, discTotal);
        }

        /// <summary>
        /// Finds an iTunes number-pair atom (<c>trkn</c> / <c>disk</c>) and reads its number and total.
        /// Layout: [size]["trkn"] [size]["data"] [type/flags 4] [locale 4] [reserved 2] [number 2] [total 2] ...
        /// </summary>
        private static bool TryReadPair(ReadOnlySpan<byte> file, ReadOnlySpan<byte> name, out int number, out int total)
        {
            number = 0; total = 0;
            int offset = 0;
            while (offset < file.Length)
            {
                int i = file[offset..].IndexOf(name);
                if (i < 0) return false;
                i += offset;
                offset = i + 4;
                // Must be a real atom: a sane size before it and a "data" child right after.
                if (i < 4 || i + 26 > file.Length) continue;
                uint atomSize = BinaryPrimitives.ReadUInt32BigEndian(file.Slice(i - 4, 4));
                if (atomSize < 30 || atomSize > 64) continue;
                if (!file.Slice(i + 8, 4).SequenceEqual("data"u8)) continue;
                int n = BinaryPrimitives.ReadUInt16BigEndian(file.Slice(i + 22, 2));
                int tot = BinaryPrimitives.ReadUInt16BigEndian(file.Slice(i + 24, 2));
                if (n <= 0) continue;
                number = n; total = tot;
                return true;
            }
            return false;
        }
    }
}
