using Melon.Classes;
using Melon.LocalClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Init
            Console.ForegroundColor = ConsoleColor.White;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            StateManager.ParseArgs(args);
            try
            {
                StateManager.Version = StateManager.LaunchArgs["SetVer"];
            }
            catch(Exception)
            {
                StateManager.Version = "1.0.0.0";
            }
            StateManager.Init(null, false, true);

            // Launch DisplayHome
            DisplayManager.DisplayHome();

            return 0;
        }
    }
}
