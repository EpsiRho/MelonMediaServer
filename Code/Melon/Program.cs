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
            StateManager.Version = StateManager.LaunchArgs["SetVer"];
            StateManager.Init(null, false);

            // Launch DisplayHome
            DisplayManager.DisplayHome();

            return 0;
        }
    }
}
