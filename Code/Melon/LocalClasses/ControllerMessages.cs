using System.Threading.Channels;
using Melon.Interface;

namespace Melon.LocalClasses;

public sealed class ControllerMessages
{
    private readonly object gate = new();
    private readonly List<Channel<ControllerMessage>> subscribers = [];
    private long sequence;

    public async IAsyncEnumerable<ControllerMessage> Subscribe(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken stopping)
    {
        var channel = Channel.CreateBounded<ControllerMessage>(new BoundedChannelOptions(128)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
        lock (gate)
        {
            channel.Writer.TryWrite(new(sequence, "connected", "Read GetStatus for current server state."));
            subscribers.Add(channel);
        }
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(stopping)) yield return message;
        }
        finally { lock (gate) subscribers.Remove(channel); }
    }

    public void Publish(string topic, string message)
    {
        lock (gate)
        {
            var item = new ControllerMessage(++sequence, topic, message);
            foreach (var channel in subscribers) channel.Writer.TryWrite(item);
        }
    }
}
