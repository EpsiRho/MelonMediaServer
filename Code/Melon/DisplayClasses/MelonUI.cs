using Amazon.Runtime.Internal.Transform;
using Azure;
using Melon.DisplayClasses;
using Melon.LocalClasses;
using MongoDB.Bson;
using MongoDB.Driver;
using Pastel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.IdentityModel.Tokens;
using static Melon.LocalClasses.StateManager;
using System.Runtime.InteropServices;

namespace Melon.Classes
{
    /// <summary>
    /// This class contains frequently used UI elements
    /// </summary>
    public static class MelonUI
    {
        // Output 
        public static bool endOptionsDisplay;
        public static void BreadCrumbBar(List<string> list)
        {
            ClearConsole();
            Console.Write("[".Pastel(MelonColor.Text));
            for(int i = 0; i < list.Count(); i++)
            {
                Color clr = new Color();
                if(list.Count - i > 2)
                {
                    clr = MelonColor.BackgroundText;
                }
                else if (list.Count - i > 1)
                {
                    clr = MelonColor.ShadedText;
                }
                else
                {
                    clr = MelonColor.Melon;
                }
                Console.Write(list[i].Pastel(clr));
                if(list.Count - 1 != i)
                {
                    Console.Write("/".Pastel(MelonColor.Text));
                }
            }
            Console.WriteLine("]".Pastel(MelonColor.Text));
        }
        [DllImport("libc")]
        public static extern int system(string exec);
        public static void ClearConsole()
        {
            try
            {
                system("clear");
            }
            catch (Exception)
            {
                Console.Clear();
            }

            Console.Out.Flush();
            Console.SetCursorPosition(0, 0);
        }
        public static void ClearConsole(int left, int top, int width, int height)
        {
            Console.SetCursorPosition(left, top);
            for (int i = 0; i < height; i++)
            {
                Console.CursorTop = top + i;
                Console.CursorLeft = left;
                Console.Write(new string(' ', width));
            }
            Console.SetCursorPosition(left, top);
        }
        public static Color ColorPicker(Color CurColor)
        {
            int y = Console.CursorTop;
            MelonUI.ClearConsole(Console.CursorLeft, y, Console.WindowWidth, Console.WindowHeight - Console.CursorTop);
            Console.CursorTop = 0;
            Color NewColor = CurColor;
            int place = 0;
            int Count = 0;
            int AccelCount = 0;
            int AccelAmmount = 0;
            Stopwatch watch = new Stopwatch();
            while (true)
            {
                Console.SetCursorPosition(0, y);
                double RedPercent = (double)NewColor.R / (double)256;
                double GreenPercent = (double)NewColor.G / (double)256;
                double BluePercent = (double)NewColor.B / (double)256;
                RedPercent = RedPercent == 0 ? 0.001: RedPercent;
                GreenPercent = GreenPercent == 0 ? 0.001 : GreenPercent;
                BluePercent = BluePercent == 0 ? 0.001 : BluePercent;

                int width = Console.WindowWidth - 10;

                double RedLineFront = (width) * RedPercent;
                double RedLineBack = (width) - RedLineFront;

                double GreenLineFront = (width) * GreenPercent;
                double GreenLineBack = (width) - GreenLineFront;

                double BlueLineFront = (width) * BluePercent;
                double BlueLineBack = (width) - BlueLineFront;

                
                int sTop = Console.CursorTop;
                Console.WriteLine($"{$"{StateManager.StringsManager.GetString("OldColorDisplay")} -".Pastel(CurColor)}{$"> {StateManager.StringsManager.GetString("NewColorDisplay")}".Pastel(NewColor)}");

                string RedBar = $"{new string('-', (int)RedLineFront)}#{new string('-', (int)RedLineBack)}";
                string GreenBar = $"{new string('-', (int)GreenLineFront)}#{new string('-', (int)GreenLineBack)}";
                string BlueBar = $"{new string('-', (int)BlueLineFront)}#{new string('-', (int)BlueLineBack)}";
                Console.CursorLeft = 0;
                Color select = MelonColor.BackgroundText;
                if(place == 0) { select = MelonColor.Text; }
                Console.WriteLine($"[{RedBar.Pastel(Color.FromArgb(255, 255 - NewColor.R, 255 - NewColor.R))}] {NewColor.R.ToString("000")}".Pastel(select));
                select = MelonColor.BackgroundText;
                if (place == 1) { select = MelonColor.Text; }
                Console.WriteLine($"[{GreenBar.Pastel(Color.FromArgb(255 - NewColor.G, 255, 255 - NewColor.G))}] {NewColor.G.ToString("000")}".Pastel(select));
                select = MelonColor.BackgroundText;
                if (place == 2) { select = MelonColor.Text; }
                Console.WriteLine($"[{BlueBar.Pastel(Color.FromArgb(255 - NewColor.B, 255 - NewColor.B, 255))}] {NewColor.B.ToString("000")}".Pastel(select));

                Console.WriteLine(StateManager.StringsManager.GetString("NavigationControls").Pastel(MelonColor.BackgroundText));
                
                // Debug info for hold acceleration
                //Console.WriteLine(Count);
                //Console.WriteLine(AccelCount);
                //Console.WriteLine(AccelAmmount);
                
                watch.Start();
                var input = Console.ReadKey(intercept: true);
                watch.Stop();
                if (watch.ElapsedMilliseconds < 100)
                {
                    Count++;
                    if (Count % 5 == 0)
                    {
                        AccelCount++;
                    }
                    else if (AccelAmmount < 25)
                    {
                        AccelAmmount += AccelCount;
                    }
                }
                else
                {
                    Count = 0;
                    AccelAmmount = 0;
                    AccelCount = 0;
                }
                watch.Reset();


                if (input.Key == ConsoleKey.LeftArrow)
                {
                    switch (place)
                    {
                        case 0:
                            int NewRed = NewColor.R - (1 + AccelAmmount);
                            if (NewRed > 0)
                            {
                                NewColor = Color.FromArgb(NewRed, NewColor.G, NewColor.B);
                            }
                            else
                            {
                                NewColor = Color.FromArgb(0, NewColor.G, NewColor.B);
                            }
                            break;
                        case 1:
                            int NewGreen = NewColor.G - (1 + AccelAmmount);
                            if (NewGreen > 0)
                            {
                                NewColor = Color.FromArgb(NewColor.R, NewGreen, NewColor.B);
                            }
                            else
                            {
                                NewColor = Color.FromArgb(NewColor.R, 0, NewColor.B);
                            }
                            break;
                        case 2:
                            int NewBlue = NewColor.B - (1 + AccelAmmount);
                            if (NewBlue > 0)
                            {
                                NewColor = Color.FromArgb(NewColor.R, NewColor.G, NewBlue);
                            }
                            else
                            {
                                NewColor = Color.FromArgb(NewColor.R, NewColor.G, 0);
                            }
                            break;
                    }
                }
                else if (input.Key == ConsoleKey.RightArrow)
                {
                    switch (place)
                    {
                        case 0:
                            int NewRed = NewColor.R + (1 + AccelAmmount);
                            if (NewRed < 256)
                            {
                                NewColor = Color.FromArgb(NewRed, NewColor.G, NewColor.B);
                            }
                            else
                            {
                                NewColor = Color.FromArgb(255, NewColor.G, NewColor.B);
                            }
                            break;
                        case 1:
                            int NewGreen = NewColor.G + (1 + AccelAmmount);
                            if (NewGreen < 256)
                            {
                                NewColor = Color.FromArgb(NewColor.R, NewGreen, NewColor.B);
                            }
                            else
                            {
                                NewColor = Color.FromArgb(NewColor.R, 255, NewColor.B);
                            }
                            break;
                        case 2:
                            int NewBlue = NewColor.B + (1 + AccelAmmount);
                            if (NewBlue < 256)
                            {
                                NewColor = Color.FromArgb(NewColor.R, NewColor.G, NewBlue);
                            }
                            else
                            {
                                NewColor = Color.FromArgb(NewColor.R, NewColor.G, 255);
                            }
                            break;
                    }
                }
                else if (input.Key == ConsoleKey.UpArrow)
                {
                    if(place != 0)
                    {
                        place--;
                    }
                }
                else if (input.Key == ConsoleKey.DownArrow)
                {
                    if(place != 2)
                    {
                        place++;
                    }
                }
                else if (input.Key == ConsoleKey.Enter)
                {
                    return NewColor;
                }
                else if (input.Key == ConsoleKey.Escape)
                {
                    return CurColor;
                }
            }
        }
        public static string HiddenInput()
        {
            string Input = "";
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;
                if (key == ConsoleKey.Backspace && Input.Length > 0)
                {
                    Console.Write("\b \b");
                    Input = Input[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    Input += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);

            return Input;
        }
        public static void DisplayProgressBar(double count, double max, char foreground, char background)
        {
            try
            {
                // 8 in the am pm gang
                double progressPercentage = count / max;
                double completedWidth = (Console.WindowWidth - 4) * progressPercentage;
                double remainingWidth = (Console.WindowWidth - 4) - completedWidth;

                string progressBar = new string(foreground, (int)completedWidth);
                string backgroundBar = new string(background, (int)remainingWidth);
                Console.CursorLeft = 0;
                Console.WriteLine($"[{progressBar.Pastel(MelonColor.Highlight)}{backgroundBar.Pastel(MelonColor.ShadedText)}] ");
            }
            catch (Exception)
            {

            }

        }
        public static void DisplayProgressBar(double progressPercentage, char foreground, char background)
        {
            try
            {
                // 8 in the am pm gang
                double completedWidth = (Console.WindowWidth - 4) * progressPercentage;
                double remainingWidth = (Console.WindowWidth - 4) - completedWidth;

                string progressBar = new string(foreground, (int)completedWidth);
                string backgroundBar = new string(background, (int)remainingWidth);
                Console.CursorLeft = 0;
                Console.WriteLine($"[{progressBar.Pastel(MelonColor.Highlight)}{backgroundBar.Pastel(MelonColor.ShadedText)}] ");
            }
            catch (Exception)
            {

            }

        }
        private static bool IndeterminateProgress { get; set; }
        private static bool NeedsShutdown { get; set; }
        public static void ShowIndeterminateProgress()
        {
            if (!IndeterminateProgress)
            {
                while(NeedsShutdown)
                {

                }
                IndeterminateProgress = true;
                Thread display = new Thread(IndeterminateProgressDisplay);
                display.Start();
            }
        }
        public static void HideIndeterminateProgress()
        {
            IndeterminateProgress = false;
        }
        private static void IndeterminateProgressDisplay()
        {
            Console.CursorVisible = false;
            int count = 0;
            int x = Console.CursorLeft;
            int y = Console.CursorTop;
            bool negative = false;
            while (IndeterminateProgress)
            {
                NeedsShutdown = true;
                Console.SetCursorPosition(x, y);
                double percent = (double)count / (double)10;
                percent = percent == 0 ? 0.001 : percent;

                if(percent == double.NaN)
                {
                    percent = 0;
                }

                int width = 10;

                int LineFront = (int)((width) * percent);
                int LineBack = (width) - LineFront;

                string Bar = $"[{new string('-', LineFront)}{"#".Pastel(MelonColor.Highlight)}{new string('-', LineBack)}]".Pastel(MelonColor.Text);

                Console.Write(Bar);
                if(negative)
                {
                    count--;
                }
                else
                {
                    count++;
                }

                if (count == 0)
                {
                    negative = false;
                }
                else if (count == width)
                {
                    negative = true;
                }

                Thread.Sleep(75);
            }
            NeedsShutdown = false;
        }

        // Input
        public static string OptionPicker(List<string> Choices)
        {
            Console.CursorVisible = false;
            Thread.Sleep(200);
            int place = 0;
            int sLeft = Console.CursorLeft;
            int sTop = Console.CursorTop;
            endOptionsDisplay = false;
            Thread DisplayThread = new Thread(() =>
            {
                try
                {
                    int x = Console.WindowWidth;
                    while (!endOptionsDisplay)
                    {
                        if (endOptionsDisplay)
                        {
                            Console.CursorVisible = true;
                            return;
                        }
                        if (x != Console.WindowWidth)
                        {
                            x = Console.WindowWidth;
                            try
                            {
                                for (int i = 0; i < Choices.Count + 1; i++)
                                {
                                    Console.CursorTop = sTop + i;
                                    Console.CursorLeft = 0;
                                    Console.Write(new string(' ', x));
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                        try
                        {
                            string controls = StateManager.StringsManager.GetString("SimpleNavigationControls");
                            int conX = Console.WindowWidth - controls.Length - 4;
                            Console.CursorLeft = conX;
                            Console.CursorTop = sTop;
                            Console.Write(controls.Pastel(MelonColor.BackgroundText));
                            Console.SetCursorPosition(sLeft, sTop);
                        }
                        catch (Exception)
                        {

                        }
                        //Console.CursorTop = sTop;
                        //Console.CursorLeft = sLeft;
                        // Show Choices
                        for (int i = 0; i < Choices.Count(); i++)
                        {
                            Color clr = new Color();
                            if (i == place)
                            {
                                clr = MelonColor.Text;
                            }
                            else
                            {
                                clr = MelonColor.BackgroundText;
                            }
                            if (StateManager.MelonSettings.UseMenuColor)
                            {
                                Console.WriteLine($"• {Choices[i]}".Pastel(clr));
                            }
                            else
                            {
                                Console.WriteLine((i == place ? "> " : "• ") + Choices[i]);
                            }
                        }
                        Thread.Sleep(10);
                    }
                }
                catch (Exception)
                {

                }
            });
            DisplayThread.Start();
            while (!endOptionsDisplay)
            {
                // Get Input
                if (Console.KeyAvailable)
                {
                    var input = Console.ReadKey(intercept: true);
                    if (input.Key == ConsoleKey.UpArrow)
                    {
                        if (place != 0)
                        {
                            place--;
                        }
                    }
                    else if (input.Key == ConsoleKey.DownArrow)
                    {
                        if (place < Choices.Count() - 1)
                        {
                            place++;
                        }
                    }
                    else if (input.Key == ConsoleKey.Enter)
                    {
                        endOptionsDisplay = true;
                        Console.CursorVisible = true;
                        return Choices[place];
                    }
                    else
                    {

                    }
                }
                else
                {
                    Thread.Sleep(50);
                }

            }
            return "";
        }
        public static string StringInput(bool UsePred, bool AutoCorrect, bool FreeInput, bool ShowChoices, List<string> Choices = null)
        {
            int place = 0;
            string command = "";
            int startLeft = Console.CursorLeft;
            int startTop = Console.CursorTop;
            string prediction = "";
            while (true)
            {
                Console.CursorLeft = startLeft;
                Console.CursorTop = startTop;
                try
                {
                    string temp = Choices.Where(item => item.ToLower().StartsWith(command.ToLower())).First();
                    if (command.Length == 0)
                    {
                        temp = "";
                    }
                    if (temp != prediction)
                    {
                        string spaces = "";
                        int difference = prediction.Length - temp.Length;
                        if (difference > 0)
                        {
                            for (int i = 0; i < difference; i++)
                            {
                                spaces += " ";
                            }
                        }
                        prediction = temp += spaces;
                    }
                }
                catch (Exception)
                {

                }
                if (ShowChoices)
                {
                    foreach (var choice in Choices)
                    {
                        Color clr = new Color();
                        if (choice == prediction.Trim())
                        {
                            clr = MelonColor.Text;
                        }
                        else
                        {
                            clr = MelonColor.BackgroundText;
                        }
                        Console.WriteLine($"• {choice.Substring(place).Pastel(clr)}".Pastel(MelonColor.Text));
                    }
                }
                Console.Write($"> {command}".Pastel(MelonColor.Text));
                Console.CursorLeft = 2;
                try
                {
                    if (UsePred)
                    {
                        Console.Write($"{prediction.Substring(place).Pastel(MelonColor.BackgroundText)}".Pastel(MelonColor.Text));
                    }
                    else
                    {
                        UsePred = true;
                    }
                }
                catch (Exception)
                {

                }
                Console.Write($" ");
                Console.CursorLeft = place + 2;
                var input = Console.ReadKey(intercept: true);
                if (input.Key == ConsoleKey.Enter)
                {
                    if (FreeInput)
                    {
                        return command;
                    }
                    else if (Choices.Contains(command))
                    {
                        return command;
                    }
                    else if (Choices.Contains(prediction) && AutoCorrect)
                    {
                        return prediction;
                    }
                    else
                    {
                        if (!ShowChoices)
                        {

                        }
                    }
                }
                else if (input.Key == ConsoleKey.Backspace)
                {
                    if (command.Length > 0)
                    {
                        if (place != command.Length)
                        {
                            command = command.Remove(place - 1, 1);
                        }
                        else
                        {
                            command = command.Substring(0, command.Length - 1);
                        }
                        place--;
                    }
                }
                else if (input.Key == ConsoleKey.LeftArrow)
                {
                    if (place > 1)
                    {
                        place--;
                        UsePred = false;
                    }
                }
                else if (input.Key == ConsoleKey.RightArrow)
                {
                    if (place < command.Length)
                    {
                        place++;
                        UsePred = false;
                    }
                }
                else if (input.Key == ConsoleKey.Tab)
                {
                    command = prediction;
                    place = command.Length;
                }
                else
                {
                    if (place != command.Length)
                    {
                        command = command.Insert(place, input.KeyChar.ToString());
                    }
                    else
                    {
                        command += input.KeyChar;
                    }
                    place++;
                }
            }
        }

    }
}
