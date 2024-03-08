using ATL;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MelonUpdater.Classes
{
    public static class MelonBuildManager
    {
        public static void Build()
        {
            string buildPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater", "MelonWebApiUpdater.exe");
            if (Program.LaunchArgs.ContainsKey("buildPath"))
            {
                buildPath = Program.LaunchArgs["buildPath"];
            }

            string version = CreateVersionNumber();

            string outputPath = $"Build/{version.Replace(".", "-")}-{DateTime.Now.TimeOfDay.Seconds}.zip";
            if (Program.LaunchArgs.ContainsKey("outputPath"))
            {
                outputPath = Program.LaunchArgs["outputPath"];
            }

            if (!Directory.Exists(buildPath))
            {
                Console.WriteLine($"[!] \"{buildPath}\" does not exist!");
                return;
            }

            try
            {
                SetVersionNumber(buildPath, version);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] Build failed!");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }

            // Build Melon
            string buildOutputPath = "";
            try
            {
                buildOutputPath = BuildProject(buildPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] Build failed!");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }


            try
            {
                UIManager.ZipProgressView("Compressing to Zip");
                var t = ZipManager.CreateZip(outputPath, buildOutputPath, new Progress<double>(x=>
                    UIManager.zipPercentage = x
                ));

                t.Wait();
                UIManager.endDisplay = true;
                Thread.Sleep(500);
                Console.WriteLine($"[+] Build created at {buildOutputPath}");
                Console.WriteLine($"[+] Build Package created at {outputPath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] Build failed!");
                Console.WriteLine($"[!] {e.Message}");
                return;
            }
        }
        private static void SetVersionNumber(string projectFolderPath, string version)
        {
            string vString = $"        public const string Version = \"{version}\";";

            string[] files = System.IO.Directory.GetFiles(projectFolderPath, "*.cs", System.IO.SearchOption.AllDirectories);

            var prog = files.FirstOrDefault(x => x.EndsWith("Program.cs"));
            if (prog.IsNullOrEmpty())
            {
                throw new InvalidOperationException("Program.cs is missing!");
            }

            var txt = File.ReadAllText(prog);
            var split = txt.Contains("\r\n") ? txt.Split("\r\n") : txt.Split("\n");
            string repl = split.FirstOrDefault(x => x.Contains("public const string Version = "));

            txt = txt.Replace(repl, vString);
            File.WriteAllText(prog, txt);
        }
        private static string BuildProject(string projectFolderPath)
        {
            var projectFilePath = System.IO.Directory.GetFiles(projectFolderPath, "*.csproj").FirstOrDefault(x=>!x.Contains("Backup"));
            if (projectFilePath.IsNullOrEmpty())
            {
                throw new InvalidOperationException("No project found!");
            }

            // Load the project
            var project = new Project(projectFilePath, null, null);

            // Retrieve the SDK path
            var sdkPath = project.GetPropertyValue("MSBuildSDKsPath");
            project.SetProperty("Configuration", "Release");

            var projectCollection = new ProjectCollection();

            var buildParameters = new BuildParameters(projectCollection)
            {
                Loggers = new List<ILogger> { new BuildLogger() }
            };

            var buildRequest = new BuildRequestData(projectFilePath, project.GlobalProperties, null, new[] { "Build" }, null);

            var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

            if (buildResult.OverallResult == BuildResultCode.Success)
            {
                var outputDir = project.GetPropertyValue("OutputPath");
                var fullOutputPath = Path.Combine(projectFolderPath, outputDir);
                return fullOutputPath;
            }
            else
            {
                throw new InvalidOperationException("Build failed.");
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
            DateTime Start = new DateTime(2024, 1, 1);
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
