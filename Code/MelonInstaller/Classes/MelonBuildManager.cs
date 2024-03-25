using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static MelonInstaller.Program;

namespace MelonInstaller.Classes
{
    public static class MelonBuildManager
    {
        public static string version = "";
        public static string buildPath = "";
        public static string outputPath = "";
        public static void PrepareBuild()
        {
            // Get Build Path
            if (!Program.LaunchArgs.ContainsKey("buildPath"))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("BuildStart")}");
                return;
            }
            buildPath = Program.LaunchArgs["buildPath"];

            if (!Directory.Exists(buildPath))
            {
                Console.WriteLine($"[!] \"{buildPath}\" {StringsManager.GetString("PathMissing")}");
                return;
            }

            // Generate Version Number
            version = CreateVersionNumber();
            try
            {
                SetVersionNumber(buildPath, version);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")}");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }

            // Get Output Path
            outputPath = $"Build/{version.Replace(".", "-")}-{DateTime.Now.TimeOfDay.Seconds}.zip";
            if (Program.LaunchArgs.ContainsKey("outputPath"))
            {
                outputPath = $"{Program.LaunchArgs["outputPath"]}/{version.Replace(".", "-")}-{DateTime.Now.TimeOfDay.Seconds}.zip";
            }
            else
            {
                Directory.CreateDirectory("Build");
            }

        }
        public static void Build()
        {

            // Build Melon
            string buildOutputPath = "";
            try
            {
                buildOutputPath = BuildProject(buildPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")}");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }


            try
            {
                UIManager.ZipProgressView(StringsManager.GetString("Compress"));
                var t = ZipManager.CreateZip(outputPath, buildOutputPath, new Progress<double>(x=>
                    UIManager.zipPercentage = x
                ));

                t.Wait();
                UIManager.endDisplay = true;
                Thread.Sleep(500);
                Console.WriteLine($"[+] {StringsManager.GetString("BuildCreated")} {buildOutputPath}");
                Console.WriteLine($"[+] {StringsManager.GetString("BuildPackageCreated")} {outputPath}");
            }
            catch (Exception e)
            {
                UIManager.endDisplay = true;
                Thread.Sleep(500);
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")}");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }
        }
        private static void SetVersionNumber(string projectFolderPath, string version)
        {
            string vString = $"        public const string Version = \"{version}\";";

            string[] files = System.IO.Directory.GetFiles(projectFolderPath, "*.cs", System.IO.SearchOption.AllDirectories);

            var prog = files.FirstOrDefault(x => x.EndsWith("Program.cs"));
            if (String.IsNullOrEmpty(prog))
            {
                throw new InvalidOperationException(StringsManager.GetString("ProgramcsMissing"));
            }

            var txt = File.ReadAllText(prog);
            var split = txt.Contains("\r\n") ? txt.Split("\r\n") : txt.Split("\n");
            string repl = split.FirstOrDefault(x => x.Contains("public const string Version = "));

            txt = txt.Replace(repl, vString);
            using (var fileStream = new FileStream(prog, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(fileStream))
                {
                    writer.Write(txt);
                    writer.Flush();
                }
            }
            Thread.Sleep(1000);
        }
        private static string BuildProject(string projectFolderPath)
        {
            var projectFilePath = System.IO.Directory.GetFiles(projectFolderPath, "*.csproj").FirstOrDefault(x=>!x.Contains("Backup"));
            if (String.IsNullOrEmpty(projectFilePath))
            {
                throw new InvalidOperationException(StringsManager.GetString("ProjectMissing"));
            }

            // Stop mucking up my version number >:(
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            // Load the project
            var project = new Project(projectFilePath, null, null);

            // Retrieve the SDK path
            var sdkPath = project.GetPropertyValue("MSBuildSDKsPath");

            var globalProperties = new Dictionary<string, string>
            {
                ["Configuration"] = "Release"
            };

            foreach(var prop in project.GlobalProperties)
            {
                globalProperties.TryAdd(prop.Key, prop.Value);
            }

            var projectCollection = new ProjectCollection(globalProperties);

            var buildParameters = new BuildParameters(projectCollection)
            {
                Loggers = new List<ILogger> { new BuildLogger() }
            };


            var buildRequest = new BuildRequestData(projectFilePath, globalProperties, null, new[] { "Build" }, null);

            var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

            if (buildResult.OverallResult == BuildResultCode.Success)
            {
                var outputDir = buildResult.ResultsByTarget["Build"].Items.FirstOrDefault().ItemSpec.Replace("MelonWebApi.dll", "");
                return outputDir;
            }
            else
            {
                throw new InvalidOperationException(StringsManager.GetString("BuildFailed"));
            }
        }
        public static string CreateVersionNumber()
        {
            int major = 1;
            int minor = 0;
            int build = 0;
            int revision = 0;

            // Get Build Number
            DateTime Time = DateTime.Now;
            DateTime Start = new DateTime(2024, 1, 1); // Initial version release
            var inbetween = Time - Start;
            build = inbetween.Days;

            // Get Revision Number
            DateTime startOfDay = Time.Date;
            TimeSpan durationSinceStartOfDay = Time - startOfDay;
            revision = (int)durationSinceStartOfDay.TotalMinutes;

            string version = $"{major}.{minor}.{build}.{revision}";

            return version;
        }
    }
}
