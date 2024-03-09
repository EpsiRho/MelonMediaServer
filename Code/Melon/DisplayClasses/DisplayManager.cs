using Amazon.Util.Internal;
using Melon.DisplayClasses;
using Melon.LocalClasses;
using Pastel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Melon.LocalClasses.StateManager;

namespace Melon.Classes
{
    /// <summary>
    /// The parent display manager, displays the main menu.
    /// </summary>
    public static class DisplayManager
    {
        // Define Menu Options
        public static OrderedDictionary MenuOptions = new OrderedDictionary();
        public static OrderedDictionary UIExtensions = new OrderedDictionary();
        public static Dictionary<string, string> HelpOptions = new Dictionary<string, string>();
        public static void DisplayHome()
        {
            Console.CursorVisible = false;
            while (!StateManager.RestartServer)
            {
                MelonUI.ClearConsole();

                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle") });

                // UI Extensions
                Action[] ex = new Action[UIExtensions.Count];
                UIExtensions.Values.CopyTo(ex, 0);
                foreach (var extension in ex)
                {
                    ((Action)extension)();
                }

                // Input
                List<string> choices = new List<string>();
                foreach(var item in MenuOptions.Keys)
                {
                    choices.Add($"{item}");
                }
                var choice = MelonUI.OptionPicker(choices);

                try
                {
                    ((Action)MenuOptions[choice])();
                }
                catch (Exception)
                {

                }
                Thread.Sleep(100);
            }
        }
        public static void DisplayHelp()
        {
            Thread.Sleep(200);
            Console.WriteLine($"{StringsManager.GetString("MelonMediaServer").Pastel(MelonColor.Melon)}".Pastel(MelonColor.Text));
            Console.WriteLine($"{StringsManager.GetString("VisitGitHub")}: https://github.com/EpsiRho/MelonMediaServer".Pastel(MelonColor.Text));
            Console.WriteLine($"{StringsManager.GetString("UsageOptions")}\n".Pastel(MelonColor.Text));
            Console.WriteLine($"{StringsManager.GetString("OptionsHeader")}:".Pastel(MelonColor.Text));
            int max = HelpOptions.Keys.Max(x => x.Length);
            foreach(var option in HelpOptions.OrderBy(x=>x.Key))
            {
                string command = option.Key;
                command += new string(' ', max - command.Length);

                int descMax = Console.WindowWidth - 7 - command.Length;
                string desc = option.Value;

                if(desc.Length > descMax)
                {
                    var split = desc.Substring(0, descMax).Split(" ");
                    var temp = String.Join(" ", split, 0, split.Length - 1);
                    desc = desc.Replace(temp, "").Trim();
                    Console.WriteLine($"  {command.Pastel(MelonColor.Highlight)}   {temp.Pastel(MelonColor.Text)}");
                    while (desc != "")
                    {
                        split = desc.Substring(0, descMax < desc.Length ? descMax : desc.Length).Split(" ");
                        temp = String.Join(" ", split, 0, split.Length - 1 != 0 ? split.Length - 1 : split.Length);
                        desc = desc.Replace(temp, "").Trim();
                        Console.WriteLine($"  {new string(' ', command.Length)}   {temp.Trim().Pastel(MelonColor.Text)}");
                    }
                }
                else
                {
                    Console.WriteLine($"  {command.Pastel(MelonColor.Highlight)}   {desc.Pastel(MelonColor.Text)}");
                }

            }
        }
        public static void SetHelpOptions()
        {
            HelpOptions = new Dictionary<string, string>
            {
                { "--help", StringsManager.GetString("HelpMenu") },
                { "--headless, -h", StringsManager.GetString("LaunchNoUI") },
                { "--setup, -s", StringsManager.GetString("ForceOOBE") },
                { "--disablePlugins, -d", StringsManager.GetString("DisablePlugins") },
                { "--version, -v", StringsManager.GetString("ShowVersion") },
                { "--lang, -l", StringsManager.GetString("LaunchWithLang") },
                { "--allowConversion, -c", StringsManager.GetString("EnableDBConversion") }
            };
        }
        public static void DisplayVersionInfo()
        {
            //var assembly = Assembly.GetExecutingAssembly().GetName();
            var version = new Version(StateManager.Version);
            DateTime buildDate = new DateTime(2024, 1, 1)
                .AddDays(version.Build)
                .AddMinutes(version.Revision);

            Thread.Sleep(200);
            Console.WriteLine(StringsManager.GetString("MelonMediaServer").Pastel(MelonColor.Melon));
            Console.WriteLine($"{StringsManager.GetString("VersionLabel")}: {StringsManager.GetString("BetaLabel")} {version}".Pastel(MelonColor.Text));
            Console.WriteLine($"{StringsManager.GetString("BuildDateLabel")}: {buildDate.ToString("MM/dd/yyyy hh:mm:sstt")} ".Pastel(MelonColor.Text));
        }
    }
}
