using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonUpdater.Classes
{
    public static class ZipManager
    {
        public static void ExtractZip(string zipPath, string extractPath)
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);
        }
        public static async Task CreateZip(string zipPath, string buildFolderPath, IProgress<double> progress)
        {

            string[] allFilesToZip = Directory.GetFiles(buildFolderPath, "*.*", System.IO.SearchOption.AllDirectories);

            // You can use the size as the progress total size
            double size = allFilesToZip.Length;

            // You can use the progress to notify the current progress.
            double finished = 0;

            // To have relative paths in the zip.
            string pathToRemove = buildFolderPath;
            if(pathToRemove.EndsWith("\\") == false)
            {
                pathToRemove += "\\";
            }

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
