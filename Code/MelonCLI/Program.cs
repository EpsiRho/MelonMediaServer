using System.Text.Json;
using Melon.Interface;
using MelonLib.API;
using MelonUI.Managers;

namespace MelonCLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? data = Environment.GetEnvironmentVariable("MELON_CONTROLLER_DATA_PATH");
        bool statusOnly = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help": case "-h": Console.WriteLine("MelonCLI [--dataPath DIRECTORY] [--status]"); return 0;
                case "--dataPath" when i + 1 < args.Length: data = args[++i]; break;
                case "--status": statusOnly = true; break;
                default: Console.Error.WriteLine("Usage: MelonCLI [--dataPath DIRECTORY] [--status]"); return 2;
            }
        }
        using var lifetime = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; lifetime.Cancel(); };
        try
        {
            if (data != null) data = Path.GetFullPath(data);
            var client = new LocalControllerClient(data);
            if (statusOnly)
            {
                var status = await client.CallAsync(nameof(IMelonAdministration.GetStatus), cancellation: lifetime.Token);
                Console.WriteLine(JsonSerializer.Serialize(status)); return 0;
            }
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                Console.Error.WriteLine("Open MelonCLI in a terminal, or use --status for JSON."); return 2;
            }
            await using var window = new ConsoleWindowManager { Title = "Melon", TargetFrameRate = 20 };
            var controller = new Controller(client, data, window, lifetime.Token);
            var loop = window.RunAsync(lifetime.Token);
            var app = controller.Run();
            try { await Task.WhenAny(loop, app); }
            finally { lifetime.Cancel(); window.Stop(); }
            await Task.WhenAll(loop, app);
            return 0;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
