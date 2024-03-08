using Melon.DisplayClasses;
using MelonUpdater.Classes;
using Microsoft.Build.Locator;

namespace MelonUpdater
{
    public static class Program
    { 
        public static Dictionary<string, string> LaunchArgs;
        public static int Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            MelonColor.SetDefaults();
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            SetupMSBuild();

            ParseArgs(args);

            if(LaunchArgs.ContainsKey("update"))
            {
                Console.WriteLine($"Updating Not Implemented");
                return 1;
            }
            else if (LaunchArgs.ContainsKey("build"))
            {
                MelonBuildManager.Build();
            }

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
        }
    }
}