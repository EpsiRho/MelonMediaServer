using Melon.Classes;
using Pastel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// Allows for creating checklists to keep the user updated with current progress.
    /// </summary>
    public static class ChecklistUI
    {
        public static ConcurrentDictionary<int, KeyValuePair<string, bool>> Checklist;
        public static bool end;
        public static void ShowChecklist()
        {
            // Starts the thread
            MelonUI.ClearConsole();
            Thread.Sleep(50);
            Console.Write("\x1b[3J"); // required for linux
            Console.SetCursorPosition(0, 0);
            Thread ChThread = new Thread(ShowChecklistThread);
            ChThread.Start();
        }
        private static void ShowChecklistThread()
        {
            // Get starting x and y
            int x = Console.CursorLeft;
            int y = Console.CursorTop;

            // Set Encoding for checkmark
            Console.CursorVisible = false;
            while (!end)
            {
                Console.CursorLeft = x;
                Console.CursorTop = y;
                try
                {
                    // Show all items
                    foreach (var item in Checklist.OrderBy(x => x.Key))
                    {

                        if (item.Value.Value)
                        {
                            Console.WriteLine($"[{"✓".Pastel(MelonColor.Highlight)}] {item.Value.Key}                 ");
                        }
                        else
                        {
                            Console.WriteLine($"[ ] {item.Value.Key}                  ");
                        }
                    }
                }
                catch (Exception)
                {

                }
            }
        }
        public static void CreateChecklist(List<string> list)
        {
            // Creates the checklist from a list of strings
            ConcurrentDictionary<int, KeyValuePair<string, bool>> checklist = new ConcurrentDictionary<int, KeyValuePair<string, bool>>();
            int count = 0;
            foreach (var item in list)
            {
                checklist[count] = KeyValuePair.Create(item, false);
                count++;
            }
            Checklist = checklist;
        }
        public static void InsertInChecklist(string item, int place, bool check)
        {
            // Allows for inserting in the checklist
            ConcurrentDictionary<int, KeyValuePair<string, bool>> newlist = new ConcurrentDictionary<int, KeyValuePair<string, bool>>();
            bool added = false;
            foreach (var olditem in Checklist.OrderBy(x => x.Key))
            {
                if (olditem.Key == place)
                {
                    newlist[place] = KeyValuePair.Create(item, check);
                    newlist[olditem.Key + 1] = olditem.Value;
                    added = true;
                }
                else if (added)
                {
                    newlist[olditem.Key + 1] = olditem.Value;
                }
                else
                {
                    newlist[olditem.Key] = olditem.Value;
                }
            }
            Checklist = newlist;
        }
        public static void UpdateChecklist(int place, bool check)
        {
            // Update values in the checklist
            try
            {
                Checklist[place] = KeyValuePair.Create(Checklist[place].Key, check);
            }
            catch (Exception)
            {

            }
        }
    }
}
