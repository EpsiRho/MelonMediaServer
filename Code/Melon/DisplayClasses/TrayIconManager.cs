using H.NotifyIcon.Core;
using Melon.Classes;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32.SafeHandles;
using Pastel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Melon.DisplayClasses
{
    public static class TrayIconManager
    {
        private static TrayIconWithContextMenu trayIcon;
        private static Icon icon;
        private static Stream ConsoleStream;

        [DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        //const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008; // Optional: Prevents automatic newline on '\n'
        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        // Define the delegate type for handling console events
        public delegate bool ConsoleEventDelegate(int eventType);

        // Define the event types we're interested in
        public const int CTRL_C_EVENT = 0;
        public const int CTRL_BREAK_EVENT = 1;
        public const int CTRL_CLOSE_EVENT = 2;
        public const int CTRL_LOGOFF_EVENT = 5;
        public const int CTRL_SHUTDOWN_EVENT = 6;

        public static void ShowConsole()
        {
            if (ConsoleStream != null)
            {
                ConsoleStream.Close();
            }

            AllocConsole();

            IntPtr stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
            IntPtr stdInHandle = GetStdHandle(STD_INPUT_HANDLE);
            if (GetConsoleMode(stdOutHandle, out uint mode))
            {
                mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                SetConsoleMode(stdOutHandle, mode);
            }
            SafeFileHandle safeFileHandleOut = new SafeFileHandle(stdOutHandle, true);
            SafeFileHandle safeFileHandleIn = new SafeFileHandle(stdInHandle, true);
            FileStream fileStreamOut = new FileStream(safeFileHandleOut, FileAccess.Write);
            FileStream fileStreamIn = new FileStream(safeFileHandleIn, FileAccess.Read);
            Encoding encoding = Encoding.UTF8;
            StreamWriter standardOutput = new StreamWriter(fileStreamOut, encoding);
            StreamReader standardInput = new StreamReader(fileStreamIn, encoding);
            standardOutput.AutoFlush = true;
            standardOutput.Flush();
            Console.ForegroundColor = ConsoleColor.White;
            Console.OutputEncoding = Encoding.UTF8;
            Console.SetOut(standardOutput);
            Console.SetIn(standardInput);

            var b = SetConsoleCtrlHandler(ConsoleEventHandler, true);
            Console.WriteLine(b);
        }
        private static bool ConsoleEventHandler(int eventType)
        {
            //if (eventType == CTRL_CLOSE_EVENT)
            //{
            //    Console.WriteLine("Console window is closing, performing cleanup...");
            //    // Perform your cleanup here
            //
            //    // Return true if the event was handled
            //    return true;
            //}

            HideConsole();

            // Return false to let the default handler process the event
            return true;
        }

        public static void HideConsole()
        {
            ConsoleStream = new FileStream("ConsoleOutput.txt", FileMode.Create, FileAccess.Write);
            Encoding encoding = Encoding.UTF8;
            StreamWriter standardOutput = new StreamWriter(ConsoleStream, encoding);
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);
            FreeConsole();
        }
        public static void AddIcon()
        {
            using var iconStream = GetStream();
            icon = new Icon(iconStream);
            trayIcon = new TrayIconWithContextMenu
            {
                Icon = icon.Handle,
                ToolTip = "Melon Media Server",
            };

            trayIcon.ContextMenu = new PopupMenu
            {
                Items =
                {
                    new PopupMenuItem("Start Scan", (_, _) => 
                    {
                        Task.Run(()=>
                        {
                            DisplayManager.UIExtensions.Add("LibraryScanIndicator", () =>
                            {
                                Console.WriteLine(StateManager.StringsManager.GetString("LibraryScanInitiation").Pastel(MelonColor.Highlight));
                            });
                            MelonUI.endOptionsDisplay = true;
                            MelonScanner.StartScan(false);
                        });
                        
                    }),
                    new PopupMenuItem("Show Console", (_, _) =>
                    {
                        ShowConsole();
                    }),
                    new PopupMenuItem("Check For Updates", (_, _) => UpdateMelon()),
                    new PopupMenuItem("Exit Melon", (_, _) =>
                    {
                        trayIcon.Dispose();
                        Environment.Exit(0);
                    }),
                },
            };
            trayIcon.Create();
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);
        private static bool ShowMessageBox(string msg)
        {
            const uint MB_OKCANCEL = 0x00000001;
            const uint MB_ICONINFORMATION = 0x00000040;

            var result = MessageBox(IntPtr.Zero, msg, "Melon Media Server", MB_OKCANCEL | MB_ICONINFORMATION);

            switch (result)
            {
                case 1: // OK
                    return true;
                case 2: // Cancel
                    return false;
                default: // Closed, so cancel
                    return false;
            }
        }
        private static bool ShowMessageBox(string title, string msg)
        {
            const uint MB_OKCANCEL = 0x00000001;
            const uint MB_ICONINFORMATION = 0x00000040;

            var result = MessageBox(IntPtr.Zero, msg, title, MB_OKCANCEL | MB_ICONINFORMATION);

            switch (result)
            {
                case 1: // OK
                    return true;
                case 2: // Cancel
                    return false;
                default: // Closed, so cancel
                    return false;
            }
        }

        public static void UpdateMelon()
        {
            var release = SettingsUI.GetGithubRelease("latest").Result;
            if (release != null)
            {
                var curVersion = System.Version.Parse(StateManager.Version);
                var latestVersion = System.Version.Parse(release.tag_name.Replace("v", ""));
                if (curVersion >= latestVersion)
                {
                    ShowMessageBox("Melon is up-to-date!");
                    return;
                }

                var check = ShowMessageBox($"Version {curVersion} -> {latestVersion}", $"Release notes:\n{release.body}");
                if (check)
                {
                    try
                    {
                        var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MelonInstaller.exe");
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = updaterPath,
                            Arguments = $"-update -restart -installPath {AppDomain.CurrentDomain.BaseDirectory} -lang {StateManager.Language}",
                            UseShellExecute = false
                        };
                        Process.Start(processInfo);

                        Environment.Exit(0);
                    }
                    catch (Exception)
                    {
                        ShowMessageBox("Updater failed to launch, is the updater missing?");
                        return;
                    }
                }
            }
            else
            {
                ShowMessageBox("No update found!");
            }
        }
        public static Stream GetStream()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var fileName = "cda.ico";

            try
            {
                return assembly.GetManifestResourceStream(
                            assembly
                                .GetManifestResourceNames()
                                .Single(resourceName => resourceName.EndsWith($".{fileName}", StringComparison.InvariantCultureIgnoreCase)))
                        ?? throw new ArgumentException($"\"{fileName}\" is not found in embedded resources");
            }
            catch (InvalidOperationException exception)
            {
                throw new ArgumentException(
                    "Not a single one was found or more than one resource with the given name was found. " +
                    "Make sure there are no collisions and the required file has the attribute \"Embedded resource\"",
                    exception);
            }
        }
        private static void ShowMessage(TrayIcon trayIcon, string message)
        {
            Console.WriteLine(message);
        }

        
        public static void RemoveIcon()
        {
            trayIcon.Remove();
            icon.Dispose();
        }
    }
}
