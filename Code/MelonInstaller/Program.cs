using MelonInstaller.Classes;
using Microsoft.Build.Locator;
using System.Diagnostics;
using System.Resources;
using System.Runtime.InteropServices;

namespace MelonInstaller
{
    public static class Program
    { 
        public static Dictionary<string, string> LaunchArgs;
        public static string installPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/MelonInstall";
        public static string versionToFind = "latest";
        public static bool addAppIcons = false;
        public static string Version = "1.1.5";
        public static ResourceManager StringsManager { get; set; }
        [DllImport("libc")]
        public static extern int system(string exec);
        public static void ClearConsole()
        {
            try
            {
                system("clear");
            }
            catch (Exception)
            {
                Console.Clear();
            }

            Console.Out.Flush();
            Console.SetCursorPosition(0, 0);
        }
        public static async Task<int> Main(string[] args)
        {
            ClearConsole();
            ParseArgs(args);

            if (LaunchArgs.ContainsKey("v") || LaunchArgs.ContainsKey("version"))
            {
                Console.WriteLine($"Melon Installer {Version}");
                return 0;
            }

            var resources = typeof(Program).Assembly.GetManifestResourceNames();
            if (LaunchArgs.ContainsKey("lang") && resources.Contains($"MelonInstaller.Strings.UIStrings{LaunchArgs["lang"].ToUpper()}.resources"))
            {
                StringsManager = new ResourceManager($"MelonInstaller.Strings.UIStrings{LaunchArgs["lang"].ToUpper()}", typeof(Program).Assembly);
            }
            else
            {
                StringsManager = new ResourceManager($"MelonInstaller.Strings.UIStringsEN", typeof(Program).Assembly);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Process[] processes = Process.GetProcessesByName("MelonWebApi.exe");
            bool isRunning = processes.Length > 0;

            string msg = StringsManager.GetString("WaitForClose");
            while (isRunning)
            {
                if (msg != "")
                {
                    Console.WriteLine($"[-] {msg}");
                    msg = "";
                }
            }


            if(LaunchArgs.ContainsKey("update"))
            {
                Thread.Sleep(1000);
                await MelonInstallManager.Install(true);
                return 1;
            }
            else if (LaunchArgs.ContainsKey("build"))
            {
                SetupMSBuild();
                MelonBuildManager.PrepareBuild();
                MelonBuildManager.Build();
                return 2;
            }

            // Install UI: Get install path
            if (!LaunchArgs.ContainsKey("installPath"))
            {
                Console.WriteLine(StringsManager.GetString("InstallPathRequest"));
                Console.WriteLine($"({StringsManager.GetString("DefaultPrompt")} {installPath})");
                var input = Console.ReadLine();

                if(Directory.CreateDirectory(input).Exists)
                {
                    installPath = input;
                }
                else
                {
                    Console.WriteLine($"[!] {StringsManager.GetString("InvalidLocation")}");
                    return 3;
                }
            }
            else
            {
                if (Directory.CreateDirectory(LaunchArgs["installPath"]).Exists)
                {
                    installPath = LaunchArgs["installPath"];
                }
                else
                {
                    Console.WriteLine($"[!] {StringsManager.GetString("InvalidLocation")}");
                    return 3;
                }
            }

            await MelonInstallManager.Install(false);

            return 0;
        }
        private static void SetupMSBuild()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                var instance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(
                                instance => instance.Version).First();
                MSBuildLocator.RegisterDefaults();
            }
        }
        private static void ParseArgs(string[] args)
        {
            LaunchArgs = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Replace("-", "").Replace("\"", "");
                if (!args[i].StartsWith("-"))
                {
                    LaunchArgs[LaunchArgs.Last().Key] += $" {arg}";
                    break;
                }

                if (i + 1 >= args.Length)
                {
                    LaunchArgs.Add(arg, "");
                    break;
                }


                if (args[i + 1].StartsWith("-"))
                {
                    LaunchArgs.Add(arg, "");
                }
                else
                {
                    string parameter = args[i + 1];
                    LaunchArgs.Add(arg, parameter);
                    i++;
                }
            }

            if (LaunchArgs.ContainsKey("installPath"))
            {
                installPath = LaunchArgs["installPath"];
            }

            if (LaunchArgs.ContainsKey("versionToFind"))
            {
                versionToFind = LaunchArgs["versionToFind"];
            }

            if (LaunchArgs.ContainsKey("addAppIcons"))
            {
                addAppIcons = true;
            }
        }
    }
}