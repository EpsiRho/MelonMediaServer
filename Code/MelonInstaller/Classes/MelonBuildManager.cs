using Melon.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using static MelonInstaller.Program;

namespace MelonInstaller.Classes
{
    /// <summary>
    /// Builds a release bundle (Server, Controller, CLI, Installer) and numbers it.
    /// Each branch counts its own builds in BuildVersions.json: the first build on a base version is base.0, and every
    /// build after it adds one to the last number. The file is committed with the source, never read from GitHub.
    /// </summary>
    public static class MelonBuildManager
    {
        private static string codePath = "";
        private static string versionFile = "";
        private static string outputPath = "";
        private static ReleaseTag release = null!;

        public static bool PrepareBuild()
        {
            if (!LaunchArgs.TryGetValue("buildPath", out var buildPath) || !Directory.Exists(buildPath))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("BuildPathRequirementPrompt")}");
                return false;
            }
            // buildPath may be the Code folder or the MelonWebApi project inside it.
            var dir = new DirectoryInfo(buildPath);
            codePath = Directory.Exists(Path.Combine(dir.FullName, "MelonWebApi")) ? dir.FullName : dir.Parent!.FullName;
            if (!File.Exists(Path.Combine(codePath, "MelonWebApi", "Program.cs")))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("ProgramcsMissing")}");
                return false;
            }

            versionFile = LaunchArgs.GetValueOrDefault("versionFile", Path.Combine(codePath, "MelonInstaller", "BuildVersions.json"));
            var branch = LaunchArgs.GetValueOrDefault("branch", ReleaseTag.FallbackBranch).Trim();
            if (!ReleaseTag.TryParse($"{branch}-0.0.0.0", out _))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InvalidBranch")} {branch}");
                return false;
            }
            var next = NextVersion(branch, LaunchArgs.GetValueOrDefault("baseVersion"));
            if (next == null)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InvalidVersion")}");
                return false;
            }
            release = new ReleaseTag(branch, next);

            var output = LaunchArgs.GetValueOrDefault("outputPath", "Build");
            Directory.CreateDirectory(output);
            outputPath = Path.Combine(output, $"Melon-{release.Tag}-{RuntimeIdentifier}.zip");
            Console.WriteLine($"[+] {StringsManager.GetString("BuildingVersion")} {release.Title} ({RuntimeIdentifier})");
            Console.WriteLine();
            return true;
        }

        public static bool Build()
        {
            var program = Path.Combine(codePath, "MelonWebApi", "Program.cs");
            var original = File.ReadAllText(program);
            var stage = Path.Combine(Path.GetTempPath(), "MelonBuild-" + Guid.NewGuid().ToString("N"));
            // dotnet publish is noisy, so its output goes here instead of the screen. Kept only if the build fails.
            var log = Path.ChangeExtension(outputPath, ".log");
            if (File.Exists(log)) File.Delete(log);

            // Each step is one line on screen: "[2/5] CLI    14s".
            var steps = new List<(string Name, Action<ProgressLine> Run)>
            {
                ("Server", _ => Publish("MelonWebApi", Path.Combine(stage, "Server"), log)),
                ("CLI", _ => Publish("MelonCLI", Path.Combine(stage, "CLI"), log)),
                ("Installer", _ => Publish("MelonInstaller", Path.Combine(stage, "Installer"), log)),
            };
            if (OperatingSystem.IsWindows())
                steps.Add(("Controller", _ => Publish("MelonGUI", Path.Combine(stage, "Controller"), log, "-p:Platform=x64")));
            steps.Add(("Zip", progress => Zip(stage, progress)));

            var total = Stopwatch.StartNew();
            ProgressLine? current = null;
            try
            {
                File.WriteAllText(program, StampVersion(original, release));
                for (var i = 0; i < steps.Count; i++)
                {
                    current = new ProgressLine($"[{i + 1}/{steps.Count}] {steps[i].Name}");
                    steps[i].Run(current);
                    current.Finish();
                }

                SaveVersion(release);
                File.Delete(log);
                var size = new FileInfo(outputPath).Length / 1024d / 1024d;
                Console.WriteLine();
                Console.WriteLine($"[+] {StringsManager.GetString("BuildPackageCreated")} {Path.GetFullPath(outputPath)} ({size:0} MB, {ProgressLine.Seconds(total.Elapsed)})");
                Console.WriteLine($"[+] {StringsManager.GetString("ReleaseTagHint")} {release.Tag} ({release.Title})");
                return true;
            }
            catch (Exception e)
            {
                current?.Finish("X");
                Console.WriteLine();
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")} {e.Message}");
                if (File.Exists(log))
                {
                    // The lines that say what broke, so you don't have to open the log for the usual cases.
                    var errors = File.ReadLines(log).Where(l => l.Contains(" error ", StringComparison.OrdinalIgnoreCase)).Distinct().Take(10).ToList();
                    foreach (var line in errors) Console.WriteLine($"    {line.Trim()}");
                    Console.WriteLine($"[!] {StringsManager.GetString("BuildLog")} {Path.GetFullPath(log)}");
                }
                return false;
            }
            finally
            {
                File.WriteAllText(program, original);
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }

        private static void Zip(string stage, ProgressLine progress)
        {
            foreach (var file in new[] { "LICENSE", "NOTICE" })
            {
                var source = Path.Combine(codePath, "..", file);
                if (File.Exists(source)) File.Copy(source, Path.Combine(stage, file));
            }
            if (File.Exists(outputPath)) File.Delete(outputPath);
            ZipManager.CreateZip(outputPath, stage, new Progress<double>(progress.Report)).Wait();
        }

        /// <summary>The next version on <paramref name="branch"/>; a new base restarts the count at base.0.</summary>
        public static Version? NextVersion(string branch, string? baseOverride)
        {
            var root = ReadVersions();
            var baseText = baseOverride ?? root["base"]?.GetValue<string>() ?? "0.9.0";
            if (!System.Version.TryParse(baseText, out var baseVersion) || baseVersion.Build < 0) return null;
            if (baseVersion.Revision >= 0) return baseVersion; // a full version was given: use it as is

            var lastText = root["branches"]?[branch]?.GetValue<string>();
            if (lastText != null && System.Version.TryParse(lastText, out var last) &&
                last.Major == baseVersion.Major && last.Minor == baseVersion.Minor && last.Build == baseVersion.Build)
                return new System.Version(last.Major, last.Minor, last.Build, last.Revision + 1);
            return new System.Version(baseVersion.Major, baseVersion.Minor, baseVersion.Build, 0);
        }

        private static JsonObject ReadVersions() =>
            File.Exists(versionFile) ? JsonNode.Parse(File.ReadAllText(versionFile))!.AsObject() : new JsonObject { ["base"] = "0.9.0" };

        private static void SaveVersion(ReleaseTag built)
        {
            var root = ReadVersions();
            var branches = root["branches"] as JsonObject ?? new JsonObject();
            branches[built.Branch] = built.Version.ToString();
            root["branches"] = branches;
            File.WriteAllText(versionFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }

        private static string StampVersion(string source, ReleaseTag tag)
        {
            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("public const string Version = "))
                    lines[i] = $"        public const string Version = \"{tag.Version}\";{(lines[i].EndsWith('\r') ? "\r" : "")}";
                else if (lines[i].Contains("public const string Branch = "))
                    lines[i] = $"        public const string Branch = \"{tag.Branch}\";{(lines[i].EndsWith('\r') ? "\r" : "")}";
            }
            return string.Join('\n', lines);
        }

        private static string RuntimeIdentifier => RuntimeInformation.RuntimeIdentifier.StartsWith("win") ? "win-x64"
            : OperatingSystem.IsMacOS() ? "osx-arm64" : "linux-x64";

        /// <summary>
        /// Publishes one project for release. Every app carries its own copy of .NET, so nobody has to install it,
        /// and is bundled into a single exe. Native libraries (SkiaSharp, LAME, WinUI) can't be bundled, so they sit
        /// next to the exe, along with anything a project marks ExcludeFromSingleFile (see MelonWebApi.csproj).
        /// </summary>
        private static void Publish(string project, string output, string log, params string[] extra)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            string[] args =
            [
                "publish", Path.Combine(codePath, project, $"{project}.csproj"),
                "-c", "Release", "-r", RuntimeIdentifier, "-o", output,
                "--self-contained", "-p:PublishSingleFile=true",
                "-p:DebugType=none",                 // no .pdb files in the release
                "-p:SatelliteResourceLanguages=en",  // skip translated error messages from libraries; Melon's own strings are built in
                "-nologo", "-v", "minimal", "-tl:off",
                .. extra,
            ];
            foreach (var arg in args) start.ArgumentList.Add(arg);

            using var writer = new StreamWriter(log, append: true);
            writer.WriteLine($"> dotnet {string.Join(' ', args)}");
            using var process = Process.Start(start) ?? throw new IOException("Could not start dotnet.");
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"{project}: dotnet publish exited with {process.ExitCode}.");
        }
    }
}
