using System.Diagnostics;
using System.Text;
using Melon.Controllers;

namespace MelonCLI;

internal static class Platform
{
    private static string? Find(string name) => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator).Where(path => Path.IsPathFullyQualified(path)).Select(path => Path.Combine(path, name)).FirstOrDefault(File.Exists);

    private static ProcessStartInfo Command(string file, params string[] args)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }

    private static async Task<int> Silent(ProcessStartInfo start, CancellationToken token)
    {
        start.RedirectStandardOutput = start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new IOException(start.FileName);
        await Task.WhenAll(process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, token),
            process.StandardError.BaseStream.CopyToAsync(Stream.Null, token), process.WaitForExitAsync(token));
        return process.ExitCode;
    }

    public static async Task Start(string? data, CancellationToken token)
    {
        if (OperatingSystem.IsWindows()) { await WindowsHost.StartAsync(data, "MelonCLI", token); return; }
        if (data == null && OperatingSystem.IsLinux() && Find("systemctl") is string systemctl)
        {
            foreach (string[] scope in new string[][] { ["--user"], [] })
            {
                if (await Silent(Command(systemctl, [.. scope, "cat", "melon.service"]), token) != 0) continue;
                if (await Silent(Command(systemctl, [.. scope, "start", "melon.service"]), token) != 0)
                    throw new IOException("Could not start melon.service.");
                return;
            }
        }
        if (data == null && OperatingSystem.IsMacOS() && File.Exists("/Library/LaunchDaemons/com.melon.server.plist"))
        {
            if (await Silent(Command("/bin/launchctl", "kickstart", "system/com.melon.server"), token) != 0)
                throw new IOException("Could not start com.melon.server.");
            return;
        }
        var exe = ServerLocator.Find("MelonCLI") ?? Find("MelonWebApi") ?? throw new ServerNotFoundException();
        // Detach the host from the terminal. Paths are positional arguments, never shell source.
        var start = Command("/bin/sh", "-c", "nohup \"$@\" </dev/null >/dev/null 2>&1 &", "melon", exe);
        start.WorkingDirectory = Path.GetDirectoryName(exe)!;
        if (data != null) { start.ArgumentList.Add("--dataPath"); start.ArgumentList.Add(data); }
        if (await Silent(start, token) != 0) throw new IOException("Could not start Melon.");
    }

    public static Task Open(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        return Launch(Path.GetFullPath(path));
    }

    public static Task Launch(string target)
    {
        var start = OperatingSystem.IsWindows() ? new ProcessStartInfo(target) { UseShellExecute = true } :
            Command(OperatingSystem.IsMacOS() ? "/usr/bin/open" : "xdg-open", target);
        using var process = Process.Start(start); return Task.CompletedTask;
    }

    public static async Task Copy(string value, CancellationToken token)
    {
        var start = OperatingSystem.IsWindows() ? Command(Path.Combine(Environment.SystemDirectory, "clip.exe")) :
            OperatingSystem.IsMacOS() ? Command("/usr/bin/pbcopy") :
            Find("wl-copy") is string wl ? Command(wl) : Command("xclip", "-selection", "clipboard");
        start.RedirectStandardInput = true;
        start.StandardInputEncoding = OperatingSystem.IsWindows() ? new UnicodeEncoding(false, false) : new UTF8Encoding(false);
        using var proc = Process.Start(start) ?? throw new IOException("Clipboard unavailable.");
        await proc.StandardInput.WriteAsync(value); proc.StandardInput.Close();
        await proc.WaitForExitAsync(token);
        if (proc.ExitCode != 0) throw new IOException("Clipboard unavailable.");
    }
}
