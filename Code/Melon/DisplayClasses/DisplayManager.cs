using Amazon.Util.Internal;
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
        public static OrderedDictionary UIExtensions = new OrderedDictionary();
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
    }
}
