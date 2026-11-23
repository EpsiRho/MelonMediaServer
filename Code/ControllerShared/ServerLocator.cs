namespace Melon.Controllers;

/// <summary>Thrown when the controllers can't find MelonWebApi, so they can ask where Melon is installed.</summary>
internal sealed class ServerNotFoundException() : FileNotFoundException("Melon's server (MelonWebApi) was not found.");

/// <summary>
/// Finds MelonWebApi for MelonGUI and MelonCLI. An install looks like this:
///   MelonInstall/Server/MelonWebApi(.exe)
///   MelonInstall/Controller/MelonGUI.exe
///   MelonInstall/CLI/MelonCLI(.exe)
/// so the server is normally one folder up from the controller, in Server. If it's somewhere else, the user can
/// point at the install once and it's remembered (checked after the folders near the app).
/// </summary>
internal static class ServerLocator
{
    public static string ExeName => OperatingSystem.IsWindows() ? "MelonWebApi.exe" : "MelonWebApi";

    private static string LocalData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Where a picked location is remembered. Shared, so pointing either controller at it fixes both.</summary>
    private static string SavedFile => Path.Combine(LocalData, "MelonControllers", "server-path.txt");

    /// <summary>The server to start, or null when it can't be found.</summary>
    /// <param name="project">The controller's project name, used to find the server's build output when running from source.</param>
    public static string? Find(string project) => Candidates(project).FirstOrDefault(File.Exists);

    private static IEnumerable<string> Candidates(string project)
    {
        // 1. Next to this app: its own folder, a sibling Server folder (the normal install), or the folder above.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        yield return Path.Combine(here.FullName, ExeName);
        if (here.Parent != null)
        {
            yield return Path.Combine(here.Parent.FullName, "Server", ExeName);
            yield return Path.Combine(here.Parent.FullName, ExeName);
        }

        // 2. Running from source: the server's build output beside this project.
        for (var cursor = here; cursor != null; cursor = cursor.Parent)
        {
            if (!File.Exists(Path.Combine(cursor.FullName, project + ".csproj"))) continue;
            var bin = Path.Combine(cursor.Parent!.FullName, "MelonWebApi", "bin");
            var sep = Path.DirectorySeparatorChar;
            var preferred = here.FullName.Contains($"{sep}Debug{sep}", StringComparison.OrdinalIgnoreCase) ? "Debug" : "Release";
            foreach (var config in new[] { preferred, "Debug", "Release" })
                yield return Path.Combine(bin, config, "net10.0", ExeName);
            break;
        }

        // 3. Wherever the user pointed us last time it couldn't be found.
        var saved = Saved();
        if (saved != null) yield return saved;

        // 4. The installer's default folder, new layout first, then the old one with everything in one folder.
        yield return Path.Combine(LocalData, "MelonInstall", "Server", ExeName);
        yield return Path.Combine(LocalData, "MelonInstall", ExeName);
    }

    /// <summary>
    /// Takes whatever the user pointed at (the install folder, its Server folder, or the exe itself), and returns the
    /// server inside it, or null if there isn't one there.
    /// </summary>
    public static string? Resolve(string? picked)
    {
        if (string.IsNullOrWhiteSpace(picked)) return null;
        picked = Path.GetFullPath(Environment.ExpandEnvironmentVariables(picked.Trim().Trim('"')));
        string[] options = [picked, Path.Combine(picked, ExeName), Path.Combine(picked, "Server", ExeName)];
        return options.FirstOrDefault(p => File.Exists(p) && Path.GetFileName(p).Equals(ExeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Remembers <paramref name="exe"/> so later launches find it first.</summary>
    public static void Save(string exe)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavedFile)!);
        File.WriteAllText(SavedFile, exe);
    }

    private static string? Saved()
    {
        try { return File.Exists(SavedFile) ? File.ReadAllText(SavedFile).Trim() : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
