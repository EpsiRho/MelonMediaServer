using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace MelonInstaller.Classes;

public static class WindowsBundleUpdate
{
    public const string AssetName = "Melon-win-x64.zip";

    public static async Task Prepare(HttpClient http, string rootPath, string? url, string? digest, string dataDirectory,
        int serverProcess, bool service, bool restart = true, string? localArchive = null)
    {
        var root = new DirectoryInfo(rootPath);
        if (!File.Exists(Path.Combine(root.FullName, "Server", "MelonWebApi.exe")) ||
            !File.Exists(Path.Combine(root.FullName, "Controller", "MelonGUI.exe")))
            throw new InvalidOperationException("Install the Windows release bundle to update Melon and its controller together.");
        if (localArchive == null && (url == null || digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) != true))
            throw new InvalidOperationException("The Windows release bundle is not available yet.");

        var stage = root.CreateSubdirectory(".melon-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var zip = localArchive ?? Path.Combine(stage.FullName, "release.zip");
            if (localArchive == null)
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using var output = File.Create(zip);
                await response.Content.CopyToAsync(output);
                output.Position = 0;
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(output));
                if (!hash.Equals(digest![7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The Windows release checksum did not match.");
            }
            var payload = Path.Combine(stage.FullName, "payload");
            ZipFile.ExtractToDirectory(zip, payload);
            foreach (var path in new[] { @"Server\MelonWebApi.exe", @"Controller\MelonGUI.exe" })
                if (!File.Exists(Path.Combine(payload, path))) throw new InvalidDataException("Incomplete Windows release.");
            if ((Directory.Exists(Path.Combine(payload, "CLI")) || File.Exists(Path.Combine(root.FullName, "CLI", "MelonCLI.exe"))) &&
                !File.Exists(Path.Combine(payload, "CLI", "MelonCLI.exe"))) throw new InvalidDataException("The Windows release is missing MelonCLI.");
            var script = Path.Combine(stage.FullName, "Update.ps1");
            using (var source = typeof(WindowsBundleUpdate).Assembly.GetManifestResourceStream("MelonInstaller.UpdateWindows.ps1")!)
            using (var output = File.Create(script)) source.CopyTo(output);
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root.FullName };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
                "-ServerProcess", serverProcess.ToString(), "-DataDirectory", dataDirectory })
                start.ArgumentList.Add(arg);
            if (!restart) start.ArgumentList.Add("-LeaveStopped");
            if (service) start.ArgumentList.Add("-Service");
            using var updater = Process.Start(start) ?? throw new IOException("Could not start the updater.");
        }
        catch { stage.Delete(recursive: true); throw; }
    }
}
