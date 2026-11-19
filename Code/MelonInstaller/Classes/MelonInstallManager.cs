using MelonInstaller.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
            if (update && OperatingSystem.IsWindows() && File.Exists(Path.Combine(installPath, "Server", "MelonWebApi.exe")))
            {
                string? archive = LaunchArgs.GetValueOrDefault("localPath");
                var asset = archive == null ? (await GetGithubRelease(versionToFind))?.assets.FirstOrDefault(a => a.name == WindowsBundleUpdate.AssetName) : null;
                var exe = Path.GetFullPath(Path.Combine(installPath, "Server", "MelonWebApi.exe"));
                using var server = Process.GetProcessesByName("MelonWebApi").FirstOrDefault(p =>
                    string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase));
                var data = LaunchArgs.GetValueOrDefault("dataPath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Melon"));
                await WindowsBundleUpdate.Prepare(httpClient, installPath, asset?.browser_download_url, asset?.digest, data,
                    server?.Id ?? 0, LaunchArgs.ContainsKey("service"), LaunchArgs.ContainsKey("restart"), archive);
                return;
            }
            string installTempDir = Path.Combine(Path.GetTempPath(), "MelonInstall-" + Guid.NewGuid().ToString("N"));
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

                var asset = OperatingSystem.IsWindows()
                    ? release.assets.FirstOrDefault(a => a.name == WindowsBundleUpdate.AssetName)
                    : null;
                asset ??= release.assets.FirstOrDefault(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && a.name != WindowsBundleUpdate.AssetName);
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
                    return;
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
            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
            var melonPath = Path.Combine(installPath, "MelonWebApi.dll");
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = melonPath,
                UseShellExecute = false
            };
            Process.Start(processInfo);

            Environment.Exit(0);
        }
    }
}
