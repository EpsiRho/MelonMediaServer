using H.NotifyIcon.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
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
        public static void AddIcon()
        {
            //string iconPath = "C:\\Users\\jhset\\Documents\\GitHub\\MelonMediaServer\\Code\\Melon\\Assets\\cda.ico";
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
                    new PopupMenuItem("Start Scan", (_, _) => ShowMessage(trayIcon, "scan")),
                    new PopupMenuItem("Show Console", (_, _) => ShowMessage(trayIcon, "console")),
                    new PopupMenuItem("Check For Updates", (_, _) => ShowMessage(trayIcon, "update")),
                    new PopupMenuItem("Exit Melon", (_, _) =>
                    {
                        trayIcon.Dispose();
                        Environment.Exit(0);
                    }),
                },
            };
            trayIcon.Create();
        }
        private static Stream GetStream()
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
