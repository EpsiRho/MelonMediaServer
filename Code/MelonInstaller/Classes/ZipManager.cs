using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonInstaller.Classes
{
    public static class ZipManager
    {
        public static Task ExtractZip(string zipPath, string extractPath, IProgress<double> progress)
        {
            return Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);
                progress.Report(1);
            });
        }
        public static async Task CreateZip(string zipPath, string buildFolderPath, IProgress<double> progress)
        {
            string[] allFilesToZip = Directory.GetFiles(buildFolderPath, "*.*", System.IO.SearchOption.AllDirectories);

            // Progress vars
            double size = allFilesToZip.Length;
            double finished = 0;


            // Create the zip file by file
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                // Go over all files and zip them.
                foreach (var file in allFilesToZip)
                {
                    // Forward slashes so the archive extracts correctly on Linux and macOS too.
                    string fileRelativePath = Path.GetRelativePath(buildFolderPath, file).Replace('\\', '/');

                    zip.CreateEntryFromFile(file, fileRelativePath);
                    finished++;
                    progress.Report(finished / size);
                }
            }
        }
    }
}
