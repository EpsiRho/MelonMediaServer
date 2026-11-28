using System.Net;
using Mono.Nat;
using Serilog;

namespace Melon.LocalClasses.Networking;

/// <summary>Asks the router to forward a port to this computer, with UPnP or NAT-PMP (whichever it answers).</summary>
public static class PortMapper
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static INatDevice? device;
    private static DateTime deviceFoundUtc;
    private const string Description = "Melon";
    /// <summary>Asked-for lease. Routers that only do permanent mappings get 0 instead.</summary>
    public const int LeaseSeconds = 2 * 3600;

    /// <summary>The router, found by broadcasting on the local network for up to <paramref name="timeout"/>. Cached for 10 minutes.</summary>
    public static async Task<INatDevice?> FindRouter(TimeSpan timeout, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        try
        {
            if (device != null && DateTime.UtcNow - deviceFoundUtc < TimeSpan.FromMinutes(10)) return device;
            var found = new TaskCompletionSource<INatDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
            INatDevice? pmp = null;
            void OnFound(object? sender, DeviceEventArgs e)
            {
                // UPnP routers also tell us their WAN address and list mappings; prefer them, but take NAT-PMP if that's all there is.
                if (e.Device.NatProtocol == NatProtocol.Upnp) found.TrySetResult(e.Device);
                else pmp ??= e.Device;
            }
            NatUtility.DeviceFound += OnFound;
            try
            {
                NatUtility.StartDiscovery(NatProtocol.Upnp, NatProtocol.Pmp);
                await Task.WhenAny(found.Task, Task.Delay(timeout, token));
            }
            finally { NatUtility.StopDiscovery(); NatUtility.DeviceFound -= OnFound; }
            device = found.Task.IsCompletedSuccessfully ? found.Task.Result : pmp;
            deviceFoundUtc = DateTime.UtcNow;
            return device;
        }
        finally { Gate.Release(); }
    }

    /// <summary>The network changed, so the router may have too: look for it again next time.</summary>
    public static void Forget() => device = null;

    public static string ProtocolName(INatDevice d) => d.NatProtocol == NatProtocol.Upnp ? "UPnP" : "NAT-PMP";

    public static async Task<IPAddress?> RouterWanAddress(INatDevice d)
    {
        try { return await d.GetExternalIPAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is MappingException or TimeoutException or System.Net.Sockets.SocketException) { return null; }
    }

    /// <summary>Forwards <paramref name="external"/> on the router to <paramref name="internalPort"/> here. Returns null on success, or what went wrong.</summary>
    public static async Task<string?> Map(int internalPort, int external, CancellationToken token)
    {
        var router = await FindRouter(TimeSpan.FromSeconds(4), token);
        if (router == null) return "The router didn't answer. It may not support automatic port opening, or it's turned off in its settings.";
        foreach (var lease in new[] { LeaseSeconds, 0 })
        {
            try
            {
                await router.CreatePortMapAsync(new Mapping(Protocol.Tcp, internalPort, external, lease, Description)).WaitAsync(TimeSpan.FromSeconds(8), token);
                return null;
            }
            catch (MappingException ex) when (lease != 0)
            {
                // 725 OnlyPermanentLeasesSupported is common on older UPnP routers.
                Log.Debug("Port mapping with a lease failed ({Code}); trying a permanent one.", ex.ErrorCode);
            }
            catch (MappingException ex)
            {
                return (int)ex.ErrorCode == 718
                    ? $"Port {external} on the router is already forwarded to another device. Choose a different outside port."
                    : $"The router refused to open port {external} ({ex.ErrorCode}: {ex.ErrorText}).";
            }
            catch (TimeoutException) { return "The router stopped answering while opening the port."; }
        }
        return "The router refused to open the port.";
    }

    public static async Task Unmap(int internalPort, int external)
    {
        try
        {
            var router = await FindRouter(TimeSpan.FromSeconds(3), CancellationToken.None);
            if (router != null) await router.DeletePortMapAsync(new Mapping(Protocol.Tcp, internalPort, external)).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is MappingException or TimeoutException or System.Net.Sockets.SocketException)
        { Log.Debug("Couldn't remove the router port mapping: {Message}", ex.Message); }
    }
}
