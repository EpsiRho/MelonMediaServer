using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonUpdater.Classes
{
    public static class MelonUpdateManager
    {
        public static void Update()
        {
            string installPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/MelonInstall";
            if (Program.LaunchArgs.ContainsKey("installPath"))
            {
                installPath = Program.LaunchArgs["installPath"];
            }

            string zipPath = DownloadZip();

            ZipManager.ExtractZip(zipPath, installPath, new Progress<double>(x =>
                UIManager.ZipProgressView("Extracting Zip")
            ));
        }

        private static string DownloadZip()
        {
            return "";
        }
    }
}
