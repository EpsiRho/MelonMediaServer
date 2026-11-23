using Melon.Models;
using MelonInstaller.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using static MelonInstaller.Program;

namespace MelonInstaller.Classes
{
    public static class MelonInstallManager
    {
        private static readonly HttpClient httpClient = new();
        private const string repository = "EpsiRho/MelonMediaServer";
        /// <summary>Written next to the install so later runs know which branch and version are installed.</summary>
        public const string MarkerFile = "melon-release.json";

        public static async Task Install(bool update)
        {
            string? archive = LaunchArgs.GetValueOrDefault("localPath");
            GithubResponse? release = null;
            ReleaseTag? target = null;
            if (archive == null)
            {
                Console.WriteLine($"[+] {StringsManager.GetString("GithubCheck")}");
                (release, target) = await ChooseRelease(interactive: !update);
                if (release == null || target == null) return;
                if (!ConfirmBranchSwitch(ReadInstalled(), target, interactive: !update)) return;
            }

            if (update && OperatingSystem.IsWindows() && File.Exists(Path.Combine(installPath, "Server", "MelonWebApi.exe")))
            {
                var asset = release?.assets.FirstOrDefault(a => a.name == WindowsBundleUpdate.AssetName);
                var exe = Path.GetFullPath(Path.Combine(installPath, "Server", "MelonWebApi.exe"));
                using var server = Process.GetProcessesByName("MelonWebApi").FirstOrDefault(p =>
                    string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase));
                var data = LaunchArgs.GetValueOrDefault("dataPath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Melon"));
                await WindowsBundleUpdate.Prepare(httpClient, installPath, asset?.browser_download_url, asset?.digest, data,
                    server?.Id ?? 0, LaunchArgs.ContainsKey("service"), LaunchArgs.ContainsKey("restart"), archive);
                if (target != null) WriteInstalled(target);
                return;
            }

            string installTempDir = Path.Combine(Path.GetTempPath(), "MelonInstall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(installTempDir);
            var installTemp = archive ?? Path.Combine(installTempDir, "release.zip");

            if (archive == null)
            {
                var asset = release!.assets.FirstOrDefault(a => a.name == BundleAssetName)
                    ?? release.assets.FirstOrDefault(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (asset == null)
                {
                    Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                    Console.WriteLine($"[!] {StringsManager.GetString("VersionMissing")} {target!.Title} ({BundleAssetName})");
                    return;
                }
                Console.WriteLine($"[+] {StringsManager.GetString("DownloadFromGithub")!.Replace("{}", target!.Title)}");
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

            try
            {
                using (var progress = new ProgressLine(StringsManager.GetString("MelonExtracting")!))
                    ZipManager.ExtractZip(installTemp, installPath, new Progress<double>(progress.Report)).Wait();
                Console.WriteLine($"[+] {StringsManager.GetString("MelonExtracted")} {installPath.Replace("\\", "/")}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }
            finally
            {
                Directory.Delete(installTempDir, true);
            }

            if (target != null) WriteInstalled(target);

            if (update)
            {
                if (LaunchArgs.ContainsKey("restart"))
                {
                    Console.WriteLine($"[+] {StringsManager.GetString("LaunchingMelon")}");
                    RestartServer(installPath);
                }
            }
            else if (!LaunchArgs.ContainsKey("noLaunch"))
            {
                LaunchController();
            }
        }

        /// <summary>
        /// Picks the branch and version from -branch / -version, or asks. Branches come from the release tags, so a
        /// branch with no releases (Stable, for now) is never offered.
        /// </summary>
        private static async Task<(GithubResponse?, ReleaseTag?)> ChooseRelease(bool interactive)
        {
            var releases = await GetGithubReleases();
            if (releases.Count == 0)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("ReleaseInformationError")}");
                return (null, null);
            }
            var tags = releases.Select(r => r.tag_name).ToArray();
            var branches = ReleaseTag.Branches(tags);
            var installed = ReadInstalled();

            string? branch = LaunchArgs.GetValueOrDefault("branch");
            if (string.IsNullOrEmpty(branch))
            {
                var fallback = installed != null && branches.Contains(installed.Branch, StringComparer.OrdinalIgnoreCase)
                    ? installed.Branch : ReleaseTag.PreferredBranch(tags)!;
                branch = interactive && branches.Length > 1 ? Pick(StringsManager.GetString("BranchPrompt")!, branches, fallback) : fallback;
            }
            branch = branches.FirstOrDefault(b => b.Equals(branch, StringComparison.OrdinalIgnoreCase));
            if (branch == null)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InvalidBranch")} {LaunchArgs.GetValueOrDefault("branch")}");
                Console.WriteLine($"[!] {StringsManager.GetString("AvailableBranches")} {string.Join(", ", branches)}");
                return (null, null);
            }

            ReleaseTag? target;
            if (versionToFind != "latest")
            {
                target = ReleaseTag.TryParse(versionToFind, out var full) ? full
                    : System.Version.TryParse(versionToFind, out var v) ? new ReleaseTag(branch, v) : null;
            }
            else
            {
                target = ReleaseTag.Latest(tags, branch);
            }
            var release = target == null ? null : releases.FirstOrDefault(r =>
                ReleaseTag.TryParse(r.tag_name, out var t) && t.Branch.Equals(target.Branch, StringComparison.OrdinalIgnoreCase) && t.Version == target.Version);
            if (release == null)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InstallFailed")}");
                Console.WriteLine($"[!] {StringsManager.GetString("VersionMissing")} {branch} {versionToFind}");
                return (null, null);
            }
            return (release, target);
        }

        /// <summary>Switching branches to a lower version number is allowed, but only after a warning.</summary>
        private static bool ConfirmBranchSwitch(ReleaseTag? installed, ReleaseTag target, bool interactive)
        {
            if (installed == null || installed.Branch.Equals(target.Branch, StringComparison.OrdinalIgnoreCase)) return true;
            if (!ReleaseTag.IsDowngrade(installed, target)) return true;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[!] {StringsManager.GetString("DowngradeWarning")!.Replace("{from}", installed.Title).Replace("{to}", target.Title)}");
            Console.ForegroundColor = ConsoleColor.White;
            if (LaunchArgs.ContainsKey("force")) return true;
            if (!interactive || Console.IsInputRedirected)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("DowngradeForce")}");
                return false;
            }
            Console.Write($"{StringsManager.GetString("ContinuePrompt")} (y/N) ");
            return Console.ReadLine()?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static string Pick(string prompt, string[] options, string fallback)
        {
            Console.WriteLine(prompt);
            for (int i = 0; i < options.Length; i++)
                Console.WriteLine($"  {i + 1}. {options[i]}{(options[i] == fallback ? $" ({StringsManager.GetString("DefaultPrompt")?.ToLower()})" : "")}");
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out var n) && n >= 1 && n <= options.Length) return options[n - 1];
            return options.FirstOrDefault(o => o.Equals(input, StringComparison.OrdinalIgnoreCase)) ?? fallback;
        }

        public static ReleaseTag? ReadInstalled()
        {
            try
            {
                var path = Path.Combine(installPath, MarkerFile);
                if (!File.Exists(path)) return null;
                var tag = JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("tag").GetString();
                return ReleaseTag.TryParse(tag, out var r) ? r : null;
            }
            catch { return null; }
        }

        private static void WriteInstalled(ReleaseTag tag) =>
            File.WriteAllText(Path.Combine(installPath, MarkerFile),
                JsonSerializer.Serialize(new { tag = tag.Tag, branch = tag.Branch, version = tag.Version.ToString() }));

        public static string BundleAssetName => OperatingSystem.IsWindows() ? WindowsBundleUpdate.AssetName
            : OperatingSystem.IsMacOS() ? "Melon-osx-arm64.zip" : "Melon-linux-x64.zip";

        public static async Task<List<GithubResponse>> GetGithubReleases()
        {
            var all = new List<GithubResponse>();
            try
            {
                httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("MelonUpdater");
                for (int page = 1; page <= 10; page++)
                {
                    var json = await httpClient.GetStringAsync($"https://api.github.com/repos/{repository}/releases?per_page=100&page={page}");
                    var batch = JsonSerializer.Deserialize<GithubResponse[]>(json) ?? [];
                    all.AddRange(batch.Where(r => !r.draft && ReleaseTag.TryParse(r.tag_name, out _)));
                    if (batch.Length < 100) break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{StringsManager.GetString("HttpError")} {ex.Message}");
            }
            return all;
        }

        /// <summary>
        /// After a fresh install: started from a terminal, run MelonCLI in that same terminal; double-clicked (the
        /// installer owns its console), open MelonGUI and let the console close.
        /// </summary>
        private static void LaunchController()
        {
            var cli = Path.Combine(installPath, "CLI", OperatingSystem.IsWindows() ? "MelonCLI.exe" : "MelonCLI");
            var gui = Path.Combine(installPath, "Controller", "MelonGUI.exe");
            bool fromTerminal = OperatingSystem.IsWindows() ? OwnsSharedConsole() : !Console.IsInputRedirected;
            try
            {
                if (!fromTerminal && File.Exists(gui))
                {
                    Console.WriteLine($"[+] {StringsManager.GetString("LaunchingController")}");
                    Process.Start(new ProcessStartInfo(gui) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(gui)! })?.Dispose();
                }
                else if (File.Exists(cli))
                {
                    Console.WriteLine($"[+] {StringsManager.GetString("LaunchingCli")}");
                    using var process = Process.Start(new ProcessStartInfo(cli) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(cli)! });
                    process?.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] {e.Message}");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        /// <summary>True when other processes (a shell) share this console, i.e. the installer was run from a terminal.</summary>
        private static bool OwnsSharedConsole()
        {
            try { return GetConsoleProcessList(new uint[8], 8) > 1; }
            catch { return !Console.IsInputRedirected; }
        }

        private static async Task DownloadFileAsync(string downloadUrl, string targetFilePath)
        {
            using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(targetFilePath, FileMode.Create);
            await response.Content.CopyToAsync(fs);
        }

        private static void RestartServer(string installPath)
        {
            var server = Path.Combine(installPath, "Server");
            var exe = Path.Combine(server, OperatingSystem.IsWindows() ? "MelonWebApi.exe" : "MelonWebApi");
            // Releases carry their own .NET, so the server is always a native exe.
            if (!File.Exists(exe)) throw new FileNotFoundException("MelonWebApi was not found after updating.", exe);
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = server });
            Environment.Exit(0);
        }
    }
}
