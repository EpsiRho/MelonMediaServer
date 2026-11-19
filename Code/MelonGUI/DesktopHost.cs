using System.Diagnostics;
using Melon.Controllers;
using Microsoft.UI.Xaml;

namespace MelonGUI;

internal static class DesktopHost
{
    private static string ThemeFile => Path.Combine(Environment.GetEnvironmentVariable("MELON_CONTROLLER_DATA_PATH") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MelonGUI", "theme");
    public static ElementTheme ReadTheme() => File.Exists(ThemeFile) && Enum.TryParse<ElementTheme>(File.ReadAllText(ThemeFile), out var theme)
        ? theme : ElementTheme.Default;
    public static void SaveTheme(ElementTheme theme)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ThemeFile)!);
        File.WriteAllText(ThemeFile, theme.ToString());
    }
    public static Task OpenFolder(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    public static bool IsServiceRunning() => WindowsHost.IsServiceRunning();
    public static Task StopService(CancellationToken token) => WindowsHost.StopService(token);
    public static Task StartAsync(string? dataPath, CancellationToken token) => WindowsHost.StartAsync(dataPath, "MelonGUI", token);
    public static Task InstallMongo(CancellationToken token) => WindowsHost.InstallMongo(token);
}
