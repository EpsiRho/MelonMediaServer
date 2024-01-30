using Melon.LocalClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.Classes
{
    /// <summary>
    /// The parent display manager, displays the main menu.
    /// </summary>
    public static class DisplayManager
    {
        // Define Menu Options
        public static OrderedDictionary MenuOptions = new OrderedDictionary();
        public static List<Action> UIExtensions = new List<Action>();
        public static void DisplayHome()
        {
            Console.CursorVisible = false;
            while (true)
            {
                MelonUI.ClearConsole();

                // Title
                MelonUI.BreadCrumbBar(new List<string>() { StateManager.StringsManager.GetString("MelonTitle") });

                // UI Extensions
                foreach (var extension in UIExtensions.ToArray())
                {
                    extension();
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
    }
}
