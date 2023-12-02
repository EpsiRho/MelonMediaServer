using Melon.Classes;
using Melon.LocalClasses;
using Melon.Models;
using Pastel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// Handles the Console Library UI
    /// </summary>
    public static class LibraryUI
    {
        private static string SearchInput { get; set; } = "";
        private static List<string> Options = new List<string>()
        {
            "Back",
            "Artists",
            "Albums",
            "Tracks",
            "Genres"
        };
        public static async void LibrarySearch()
        {
            while (true)
            {
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Library Search (Beta)" });
                var menu = MelonUI.OptionPicker(Options);
                if (menu == "Back")
                {
                    break;
                }
                string input = "";
                MelonUI.BreadCrumbBar(new List<string>() { "Melon", "Library Search (Beta)" });
                int page = 0;
                while (true)
                {
                    Console.CursorTop = 1;
                    Console.CursorLeft = 0;
                    Console.WriteLine($"(Hit Escape to return)".Pastel(MelonColor.BackgroundText));
                    Console.WriteLine($"Search: {input} ".Pastel(MelonColor.Text));
                    var ck = Console.ReadKey(intercept: true);
                    if (ck.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                    else if (ck.Key == ConsoleKey.Backspace)
                    {
                        try
                        {
                            input = input.Substring(0, input.Length - 1);
                        }
                        catch (Exception)
                        {

                        }
                    }
                    else if (ck.Key == ConsoleKey.RightArrow)
                    {
                        page++;
                    }
                    else if (ck.Key == ConsoleKey.LeftArrow)
                    {
                        if (page != 0)
                        {
                            page--;
                        }
                    }
                    else
                    {
                        input += ck.KeyChar;
                    }
                    switch (menu)
                    {
                        case "Tracks":
                            List<Track> tracks = new List<Track>();
                            try
                            {
                                tracks = await MelonAPI.SearchTracks(input, page, Console.WindowHeight - 7);
                            }
                            catch (Exception)
                            {
                                tracks.Add(new Track() { TrackName = "No Results" });
                            }
                            Console.CursorLeft = 0;
                            int tempy = Console.CursorTop;
                            for (int y = Console.CursorTop; y < Console.WindowHeight; y++)
                            {
                                Console.SetCursorPosition(0, y);
                                Console.Write(new string(' ', Console.WindowWidth));
                            }
                            Console.CursorLeft = 0;
                            Console.CursorTop = tempy;
                            foreach (var track in tracks)
                            {
                                string write = track.TrackName;
                                try
                                {
                                    TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                                    foreach (var word in input.Split(" "))
                                    {
                                        if (word != "")
                                        {
                                            write = write.Replace(word, word.Pastel(MelonColor.Highlight));
                                            write = write.Replace(textInfo.ToTitleCase(word), textInfo.ToTitleCase(word).Pastel(MelonColor.Highlight));
                                        }
                                    }
                                    Console.WriteLine($"{write}".Pastel(MelonColor.Text));
                                }
                                catch (Exception)
                                {
                                    Console.WriteLine($"{track.TrackName}".Pastel(MelonColor.Text));
                                }
                            }
                            Console.WriteLine($"Page {page} | Showing a max of {Console.WindowHeight - 7} items".Pastel(MelonColor.ShadedText));
                            Console.WriteLine("Press any key to continue...".Pastel(MelonColor.BackgroundText));
                            break;
                    }
                }
            }
        }
    }
}
