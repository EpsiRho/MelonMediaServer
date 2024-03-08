using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonUpdater.Classes
{
    public static class ZipManager
    {
        public static async Task ExtractZip(string zipPath, string extractPath, IProgress<double> progress)
        {
            // Ensure the target directory exists
            Directory.CreateDirectory(extractPath);

            int finished = 0;

            // Open the zip file for reading
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else 
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                    progress.Report(finished / archive.Entries.Count);
                }
            }
        }
        public static async Task CreateZip(string zipPath, string buildFolderPath, IProgress<double> progress)
        {
            string[] allFilesToZip = Directory.GetFiles(buildFolderPath, "*.*", System.IO.SearchOption.AllDirectories);

            // Progress vars
            double size = allFilesToZip.Length;
            double finished = 0;

            // To have relative paths in the zip.
            string pathToRemove = buildFolderPath;
            if(pathToRemove.EndsWith("\\") == false)
            {
                pathToRemove += "\\";
            }

            // Create the zip file by file
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                // Go over all files and zip them.
                foreach (var file in allFilesToZip)
                {
                    String fileRelativePath = file.Replace(pathToRemove, "");

                    zip.CreateEntryFromFile(file, fileRelativePath);
                    finished++;
                    progress.Report(finished / size);
                }
            }
        }
    }
}
