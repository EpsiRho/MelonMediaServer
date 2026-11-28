using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Melon.Controllers;

internal static class WindowsHost
{
    public static bool IsServiceRunning()
    {
        using var service = new System.ServiceProcess.ServiceController("Melon");
        try { return service.Status is System.ServiceProcess.ServiceControllerStatus.Running or System.ServiceProcess.ServiceControllerStatus.StartPending; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public static async Task StopService(CancellationToken token)
    {
        var sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        var result = await Run(sc, ["stop", "Melon"], false, token);
        if (result == 5) result = await Run(sc, ["stop", "Melon"], true, token);
        if (result is not (0 or 1062)) throw new InvalidOperationException($"Windows service error {result}.");
    }

    public static async Task StartAsync(string? dataPath, string project, CancellationToken token)
    {
        var sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        if (dataPath == null)
        {
            var query = await Run(sc, ["query", "Melon"], false, token);
            if (query == 0)
            {
                var result = await Run(sc, ["start", "Melon"], false, token);
                if (result == 5) result = await Run(sc, ["start", "Melon"], true, token);
                if (result is not (0 or 1056)) throw new InvalidOperationException($"Windows service error {result}.");
                return;
            }
            if (query != 1060) throw new InvalidOperationException($"Windows service error {query}.");
        }
        var exe = ServerLocator.Find(project) ?? throw new ServerNotFoundException();
        var start = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false, CreateNoWindow = true };
        if (dataPath != null) { start.ArgumentList.Add("--dataPath"); start.ArgumentList.Add(dataPath); }
        using var process = Process.Start(start);
    }

    public static async Task InstallMongo(CancellationToken token)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Melon.Controllers.InstallMongo.ps1")!;
        using var reader = new StreamReader(resource);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(await reader.ReadToEndAsync(token)));
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var result = await Run(powershell, ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], true, token);
        if (result != 0) throw new InvalidOperationException(result switch
        {
            2 => "Another MongoDB setup is in progress.",
            3 => "A different service already uses the MelonMongoDB name.",
            4 => "Port 27524 is already in use.",
            5 => "MongoDB download verification failed.",
            9 => "MongoDB did not start in time.",
            _ => $"MongoDB setup failed ({result})."
        });
    }

    public const string FirewallRule = "Melon Media Server";

    /// <summary>Whether Windows Firewall has Melon's inbound rule. Reading rules doesn't need administrator rights.</summary>
    public static async Task<bool> FirewallRuleExists(CancellationToken token)
    {
        var netsh = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        return await Run(netsh, ["advfirewall", "firewall", "show", "rule", "name=" + FirewallRule], false, token) == 0;
    }

    /// <summary>
    /// Lets other devices reach Melon's ports through Windows Firewall (asks for administrator rights). Without it, Windows
    /// blocks the Melon service from the network, and other devices can't connect even on the same Wi-Fi.
    /// </summary>
    public static async Task AllowThroughFirewall(int[] ports, CancellationToken token)
    {
        var list = string.Join(",", ports.Where(p => p is > 0 and < 65536).Distinct());
        if (list.Length == 0) throw new ArgumentException("No ports to allow.");
        var netsh = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        // One elevated call (one prompt): update the rule's ports if it exists, else add it.
        string[] args = await FirewallRuleExists(token)
            ? ["advfirewall", "firewall", "set", "rule", "name=" + FirewallRule, "new", "localport=" + list, "protocol=TCP", "action=allow", "enable=yes"]
            : ["advfirewall", "firewall", "add", "rule", "name=" + FirewallRule, "dir=in", "action=allow", "protocol=TCP", "localport=" + list, "profile=any"];
        var result = await Run(netsh, args, true, token);
        if (result != 0) throw new InvalidOperationException($"Windows Firewall didn't accept the rule ({result}).");
    }

    private static async Task<int> Run(string program, string[] arguments, bool elevate, CancellationToken token)
    {
        var start = new ProcessStartInfo(program) { UseShellExecute = elevate, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden };
        if (elevate) start.Verb = "runas";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException(program);
        await process.WaitForExitAsync(token);
        return process.ExitCode;
    }
}
