using MelonInstaller.Classes;
using Microsoft.Build.Locator;
using System.Diagnostics;

namespace MelonInstaller
{
    public static class Program
    { 
        public static Dictionary<string, string> LaunchArgs;
        public static string installPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/MelonInstall";
        public static string versionToFind = "latest";
        public static bool addAppIcons = false;
        public static async Task<int> Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Process[] processes = Process.GetProcessesByName("MelonWebApi.exe");
            bool isRunning = processes.Length > 0;

            string msg = "Waiting on Melon to close...";
            while (isRunning)
            {
                if (msg != "")
                {
                    Console.WriteLine($"[-] {msg}");
                    msg = "";
                }
            }

            SetupMSBuild();

            ParseArgs(args);

            if(LaunchArgs.ContainsKey("update"))
            {
                await MelonInstallManager.Install(true);
                return 1;
            }
            else if (LaunchArgs.ContainsKey("build"))
            {
                MelonBuildManager.Build();
                return 2;
            }

            // Install UI: Get install path
            if (!LaunchArgs.ContainsKey("installPath"))
            {
                Console.WriteLine($"Welcome to Melon! Please enter an install path or nothing to install to the default location.");
                Console.WriteLine($"(The default is {installPath})");
                var input = Console.ReadLine();

                if(Directory.CreateDirectory(input).Exists)
                {
                    installPath = input;
                }
                else
                {
                    Console.WriteLine($"[!] Invalid location");
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
                    Console.WriteLine($"[!] Invalid location");
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
                string arg = args[i].Replace("-", "");
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