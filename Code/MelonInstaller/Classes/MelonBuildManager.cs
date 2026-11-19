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
        public static string branch = "";
        public static string buildPath = "";
        public static string outputPath = "";
        public static bool PrepareBuild()
        {
            // Get Build Path
            if (!Program.LaunchArgs.ContainsKey("buildPath"))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("PathMissing")}");
                return false;
            }
            buildPath = Program.LaunchArgs["buildPath"];

            if (!Directory.Exists(buildPath))
            {
                Console.WriteLine($"[!] \"{buildPath}\" {StringsManager.GetString("PathMissing")}");
                return false;
            }

            // Generate Version Number
            (version, branch) = CreateVersionNumber();
            if(string.IsNullOrEmpty(version))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")}");
                return false;
            }
            try
            {
                SetVersionNumber(buildPath, version, branch);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")}");
                Console.WriteLine($"[!] {e.Message}");
                return false;
            }

            // Get Output Path
            outputPath = $"Build/{branch}-{version.Replace(".", "-")}-{DateTime.Now.TimeOfDay.Seconds}.zip";
            if (Program.LaunchArgs.ContainsKey("outputPath"))
            {
                outputPath = $"{Program.LaunchArgs["outputPath"]}/{branch}-{version.Replace(".", "-")}-{DateTime.Now.TimeOfDay.Seconds}.zip";
            }
            else
            {
                Directory.CreateDirectory("Build");
            }

            return true;
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
        private static void SetVersionNumber(string projectFolderPath, string version, string branch)
        {
            string vString = $"        public const string Version = \"{version}\";";
            string bString = $"        public const string Branch = \"{branch}\";";

            string[] files = System.IO.Directory.GetFiles(projectFolderPath, "*.cs", System.IO.SearchOption.AllDirectories);

            var prog = files.FirstOrDefault(x => x.EndsWith("Program.cs"));
            if (String.IsNullOrEmpty(prog))
            {
                throw new InvalidOperationException(StringsManager.GetString("ProgramcsMissing"));
            }

            var txt = File.ReadAllText(prog);
            var split = txt.Contains("\r\n") ? txt.Split("\r\n") : txt.Split("\n");
            string repl = split.FirstOrDefault(x => x.Contains("public const string Version = "));
            string repl2 = split.FirstOrDefault(x => x.Contains("public const string Branch = "));

            txt = txt.Replace(repl, vString);
            txt = txt.Replace(repl2, bString);
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
                Console.WriteLine($"[!] {StringsManager.GetString("BuildFailed")}");
                Console.WriteLine($"[!] {buildResult.Exception}");
                return "";
            }
        }
        public static (string, string) CreateVersionNumber()
        {
            if (!Program.LaunchArgs.ContainsKey("buildVersion"))
            {
                Console.WriteLine($"[!] {StringsManager.GetString("MissingVersion")}");
                return ("", "");
            }

            string ver = Program.LaunchArgs["buildVersion"];
            string[] num = ver.Split(".");
            string[] branch = ver.Split("/");
            if(num.Length != 3)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InvalidVersion")}");
                return ("", "");
            }
            if (branch.Length != 2)
            {
                Console.WriteLine($"[!] {StringsManager.GetString("InvalidVersion")}");
                return ("", "") ;
            }

            var now = DateTime.UtcNow;

            string version = $"{num[0]}.{num[1]}.{num[2].Split("/")[0]}.{now.Hour}{now.Minute}{now.Second}";
            string branchName = $"{branch[1]}";

            return (version, branchName);
        }
    }
}
