using MelonInstaller.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static MelonInstaller.Program;

namespace MelonInstaller.Classes
{
    public static class MelonInstallManager
    {
        private static HttpClient httpClient = new HttpClient();
        private static string repository = "EpsiRho/MelonMediaServer";
        public static async Task Install(bool update)
        {
            string installTempDir = $"{installPath}/temp";
            Directory.CreateDirectory(installTempDir);
            var installTemp = $"{installTempDir}/release.zip";

            if (!LaunchArgs.ContainsKey("localPath"))
            {
                // Check github
                Console.WriteLine($"[+] {StringsManager.GetString("GithubCheck")}");
                var release = await GetGithubRelease(versionToFind);
                if (release == null)
                {
                    Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                    Console.WriteLine($"[!] {StringsManager.GetString("VersionMissing")} {versionToFind}");
                    return;
                }

                var asset = release.assets.FirstOrDefault();
                if (asset == null)
                {
                    Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                    Console.WriteLine($"[!] {StringsManager.GetString("VersionMissing")} {versionToFind}");
                    return;
                }
                // Download File If Newer
                Console.WriteLine($"[+] {StringsManager.GetString("DownloadFromGithub").Replace("{}",versionToFind)}");
                // Download
                try
                {
                    await DownloadFileAsync(asset.browser_download_url, installTemp);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                    Console.WriteLine($"[!] {e.Message}");
                }
            }
            else
            {
                installTemp = LaunchArgs["localPath"];
            }

            // Extract the zip file
            try
            {
                UIManager.ZipProgressView(StringsManager.GetString("MelonExtracting"));
                var t = ZipManager.ExtractZip($"{installTemp}", installPath, new Progress<double>(x =>
                    UIManager.zipPercentage = x
                ));
                t.Wait();
                UIManager.endDisplay = true;
                Thread.Sleep(500);
                Console.WriteLine($"[+] {StringsManager.GetString("MelonExtracted")} {installPath.Replace("\\","/")}");
            }
            catch (Exception e)
            {
                UIManager.endDisplay = true;
                Thread.Sleep(500);
                Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }

            // Remove the zip file
            Directory.Delete(installTempDir, true);

            if (update)
            {
                // If set, restart the server
                if (Program.LaunchArgs.ContainsKey("restart"))
                {
                    Console.WriteLine($"[+] {StringsManager.GetString("LaunchingMelon")}");
                    RestartServer(installPath);
                }
            }
            else
            {

            }
        }
        public static async Task<GithubResponse> GetGithubRelease(string version)
        {
            try
            {
                string url = "";
                if(version != "latest")
                {
                    url = $"https://api.github.com/repos/{repository}/releases/tags/{version}";
                }
                else
                {
                    url = $"https://api.github.com/repos/{repository}/releases/latest";
                }
                httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("MelonUpdater");

                var response = await httpClient.GetStringAsync(url);
                var release = JsonSerializer.Deserialize<GithubResponse>(response);
                if (release == null)
                {
                    Console.WriteLine(StringsManager.GetString("ReleaseInformationError"));
                    return null;
                }

                return release;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{StringsManager.GetString("HttpError")} {ex.Message}");
                return null;
            }
        }

        private static async Task DownloadFileAsync(string downloadUrl, string targetFilePath)
        {
            using (var response = await httpClient.GetAsync(downloadUrl))
            {
                response.EnsureSuccessStatusCode();
                await using (var fs = new FileStream(targetFilePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
        }
        private static void RestartServer(string installPath)
        {
            var melonPath = Path.Combine(installPath, "MelonWebApi.exe");
            var processInfo = new ProcessStartInfo
            {
                FileName = melonPath,
                Arguments = $"",
                UseShellExecute = false
            };
            Process.Start(processInfo);

            Environment.Exit(0);
        }
    }
}
