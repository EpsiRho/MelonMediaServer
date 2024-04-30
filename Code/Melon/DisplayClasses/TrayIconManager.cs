using Amazon.Runtime.Internal.Util;
using Amazon.Util.Internal;
using H.NotifyIcon.Core;
using Melon.Classes;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using Microsoft.Win32.SafeHandles;
using Pastel;
using Serilog;
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
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        public static void ShowConsole()
        {
            try
            {
                var uiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Melon.exe");
                string args = $"-SetVer {StateManager.Version} ";
                foreach(var arg in StateManager.LaunchArgs)
                {
                    args += $"-{arg.Key} {arg.Value} ";
                }
                var processInfo = new ProcessStartInfo
                {
                    FileName = uiPath,
                    Arguments = args,
                    UseShellExecute = false
                };
                Process.Start(processInfo);
            }
            catch (Exception)
            {
                
            }
        }
        public static void HideConsole()
        {
            FreeConsole();
        }
        public static void AddIcon()
        {
            using var iconStream = GetStream("cda.ico");
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
                    new PopupSubMenu()
                    {
                        Text = StateManager.StringsManager.GetString("ScannerOption"),
                        Items =
                        {
                            new PopupMenuItem(StateManager.StringsManager.GetString("FullScanOption"), (_, _) =>
                            {
                                Task.Run(()=>
                                {
                                    MelonScanner.StartScan(false);
                                });
                            }),
                            new PopupMenuItem(StateManager.StringsManager.GetString("ShortScanOption"), (_, _) =>
                            {
                                Task.Run(()=>
                                {
                                    MelonScanner.StartScan(true);
                                });
                            }),
                        }
                    },
                    new PopupMenuItem(StateManager.StringsManager.GetString("ShowConsoleOption"), (_, _) => Task.Run(ShowConsole)),
                    new PopupMenuItem(StateManager.StringsManager.GetString("CheckForUpdates"), (_, _) => Task.Run(UpdateMelon)),
                    new PopupMenuItem(StateManager.StringsManager.GetString("ExitMelonOption"), (_, _) =>
                    {
                        trayIcon.Dispose();
                        icon.Dispose();
                        Environment.Exit(0);
                    }),
                },
            };
            trayIcon.Create();
            trayIcon.Removed += (_, _) => Log.Warning("TrayIcon Removed");
        }
        public static void RemoveIcon()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    trayIcon.Remove();
                    icon.Dispose();
                }
                catch (Exception)
                {

                }
            }
        }
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
        public static Stream GetStream(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();


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
    }
}
