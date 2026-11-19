using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Melon.Interface;
using Melon.LocalClasses;

namespace MelonWebApi.Services;

/// <summary>One UTF-8 JSON request and reply per connection. Only the public administration contract is dispatched.</summary>
internal sealed class LocalControllerServer(ServerAdministration administration, string pipeName)
{
    private static readonly Dictionary<string, MethodInfo> Commands =
        typeof(IMelonAdministration).GetMethods().ToDictionary(method => method.Name);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal async Task RunAsync(CancellationToken stopping)
    {
        var clients = new List<Task>();
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try { await pipe.WaitForConnectionAsync(stopping); }
                catch { pipe.Dispose(); throw; }
                clients.RemoveAll(task => task.IsCompleted);
                clients.Add(Task.Run(() => ServeAsync(pipe, stopping)));
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        finally { await Task.WhenAll(clients); }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stopping)
    {
        using (pipe)
        using (var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true))
        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true })
        {
            try
            {
                var line = await reader.ReadLineAsync(stopping);
                if (line == null) return;
                using var document = JsonDocument.Parse(line);
                var request = document.RootElement;
                var command = request.GetProperty("method").GetString() ?? "";
                if (command == "Subscribe")
                {
                    await StreamMessagesAsync(reader, writer, stopping);
                    return;
                }
                if (!Commands.TryGetValue(command, out var method)) throw new ArgumentException("Unknown method.");
                request.TryGetProperty("params", out var parameters);
                var args = method.GetParameters().Select(parameter =>
                {
                    if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(parameter.Name!, out var value))
                        return value.Deserialize(parameter.ParameterType, Json) ?? throw new ArgumentException($"Null {parameter.Name}.");
                    if (parameter.HasDefaultValue) return parameter.DefaultValue;
                    throw new ArgumentException($"Missing {parameter.Name}.");
                }).ToArray();
                var result = method.Invoke(administration, args);
                if (result is Task task)
                {
                    await task;
                    result = method.ReturnType.GetProperty("Result")?.GetValue(task);
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, result }, Json));
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (IOException) when (!pipe.IsConnected) { }
            catch (Exception ex)
            {
                var cause = ex is TargetInvocationException { InnerException: {} inner } ? inner : ex;
                // Driver and certificate exceptions may include credentials or connection strings.
                var message = cause is ArgumentException or InvalidOperationException or FileNotFoundException
                    ? cause.Message : "Command failed. Check the server configuration and logs.";
                try { await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = false, error = message }, Json)); }
                catch (IOException) { }
            }
        }
    }

    private async Task StreamMessagesAsync(StreamReader reader, StreamWriter writer, CancellationToken stopping)
    {
        using var connected = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        var disconnected = WatchDisconnectAsync();
        try
        {
            await foreach (var message in administration.Messages.Subscribe(connected.Token))
            {
                var item = message.Topic == "connected" ? message with { Status = administration.GetStatus() } : message;
                await writer.WriteLineAsync(JsonSerializer.Serialize(item, Json).AsMemory(), connected.Token);
            }
        }
        catch (OperationCanceledException) when (connected.IsCancellationRequested) { }
        finally { connected.Cancel(); await disconnected; }

        async Task WatchDisconnectAsync()
        {
            try { await reader.ReadLineAsync(connected.Token); }
            catch (IOException) { }
            catch (OperationCanceledException) when (connected.IsCancellationRequested) { }
            finally { connected.Cancel(); }
        }
    }
}
