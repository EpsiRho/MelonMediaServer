using Melon.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonUpdater.Classes
{
    public static class UIManager
    {
        public static bool endDisplay = false;
        public static double zipPercentage = 0;
        public static void ZipProgressView(string action)
        {
            // Title
            Console.CursorVisible = false;
            Console.WriteLine();

            endDisplay = false;
            int sLeft = Console.CursorLeft;
            int sTop = Console.CursorTop;
            Thread DisplayThread = new Thread(() =>
            {
                int x = Console.WindowWidth;
                while (!endDisplay)
                {
                    if (endDisplay)
                    {
                        return;
                    }
                    if (x != Console.WindowWidth)
                    {
                        x = Console.WindowWidth;
                    }
                    try
                    {
                        Console.CursorTop = sTop;
                        Console.CursorLeft = sLeft;
                        var msg = $"{action}: {(zipPercentage*100).ToString("000.00")}%";
                        Console.Write(msg);
                        Console.WriteLine(new string(' ', Console.WindowWidth - msg.Length - 1));
                        MelonUI.DisplayProgressBar(zipPercentage, '#', '-');
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                        Console.WriteLine(new string(' ', Console.WindowWidth));
                    }
                    catch (Exception)
                    {

                    }
                }
            });
            DisplayThread.Priority = ThreadPriority.Highest;
            DisplayThread.Start();
        }
    }
}
