using System.IO.Pipes;
using Melon.Interface;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MelonLib.API;

/// <summary>Local administration client for a running Melon server owned by the same OS account.</summary>
public sealed class LocalControllerClient(string? dataDirectory = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string GetPipeName(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Melon");
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        if (OperatingSystem.IsWindows()) path = path.ToUpperInvariant();
        return "Melon.Control.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..24];
    }

    public async Task<JsonElement> CallAsync(string method, object? parameters = null, CancellationToken cancellation = default)
    {
        using var pipe = CreatePipe();
        await pipe.ConnectAsync(5000, cancellation);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { method, @params = parameters }, Json).AsMemory(), cancellation);
        var line = await reader.ReadLineAsync(cancellation) ?? throw new EndOfStreamException("Melon disconnected.");
        using var reply = JsonDocument.Parse(line);
        if (!reply.RootElement.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException(reply.RootElement.GetProperty("error").GetString());
        return reply.RootElement.GetProperty("result").Clone();
    }

    public async IAsyncEnumerable<ControllerMessage> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation = default)
    {
        using var pipe = CreatePipe();
        await pipe.ConnectAsync(5000, cancellation);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync("""{"method":"Subscribe"}""".AsMemory(), cancellation);
        while (await reader.ReadLineAsync(cancellation) is string line)
            yield return JsonSerializer.Deserialize<ControllerMessage>(line, Json) ?? throw new InvalidDataException("Missing event.");
    }

    private NamedPipeClientStream CreatePipe() => new(".", GetPipeName(dataDirectory), PipeDirection.InOut,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, System.Security.Principal.TokenImpersonationLevel.Identification);
}
