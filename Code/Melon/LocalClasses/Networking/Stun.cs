using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Melon.LocalClasses.Networking;

/// <summary>
/// Asks a public STUN server (RFC 5389) which address our packets come from, which is our public address. Only a binding
/// request is sent: no account, no relaying.
/// </summary>
public static class Stun
{
    public static readonly (string Host, int Port)[] Servers = [("stun.l.google.com", 19302), ("stun.cloudflare.com", 3478), ("stun1.l.google.com", 19302)];
    private const uint Cookie = 0x2112A442;

    /// <summary>The public address for <paramref name="family"/> (IPv4 or IPv6), or null when no server answered.</summary>
    public static async Task<IPAddress?> PublicAddress(AddressFamily family, CancellationToken token)
    {
        foreach (var (host, port) in Servers)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, family, token);
                if (addresses.Length == 0) continue;
                var found = await Ask(new IPEndPoint(addresses[0], port), token);
                if (found != null) return found;
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException || ex is OperationCanceledException && !token.IsCancellationRequested) { }
        }
        return null;
    }

    private static async Task<IPAddress?> Ask(IPEndPoint server, CancellationToken token)
    {
        using var udp = new UdpClient(server.AddressFamily);
        var request = new byte[20];
        BinaryPrimitives.WriteUInt16BigEndian(request, 0x0001); // Binding request, no attributes
        BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(4), Cookie);
        var transaction = RandomNumberGenerator.GetBytes(12);
        transaction.CopyTo(request, 8);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await udp.SendAsync(request, server, token);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
            wait.CancelAfter(TimeSpan.FromMilliseconds(1500));
            try
            {
                while (true)
                {
                    var reply = await udp.ReceiveAsync(wait.Token);
                    var parsed = Parse(reply.Buffer, transaction);
                    if (parsed != null) return parsed;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        }
        return null;
    }

    private static IPAddress? Parse(byte[] m, byte[] transaction)
    {
        if (m.Length < 20 || BinaryPrimitives.ReadUInt16BigEndian(m) != 0x0101 || BinaryPrimitives.ReadUInt32BigEndian(m.AsSpan(4)) != Cookie
            || !m.AsSpan(8, 12).SequenceEqual(transaction)) return null;
        int length = BinaryPrimitives.ReadUInt16BigEndian(m.AsSpan(2)), at = 20, end = Math.Min(m.Length, 20 + length);
        IPAddress? plain = null;
        while (at + 4 <= end)
        {
            int type = BinaryPrimitives.ReadUInt16BigEndian(m.AsSpan(at)), size = BinaryPrimitives.ReadUInt16BigEndian(m.AsSpan(at + 2));
            int value = at + 4;
            if (value + size > end) break;
            if ((type == 0x0020 || type == 0x0001) && size >= 8)
            {
                int family = m[value + 1];
                var bytes = m.AsSpan(value + 4, family == 1 ? 4 : 16).ToArray();
                if (family is 1 or 2 && (family == 1 ? size >= 8 : size >= 20))
                {
                    if (type == 0x0020)
                    {
                        var key = new byte[16];
                        BinaryPrimitives.WriteUInt32BigEndian(key, Cookie);
                        transaction.CopyTo(key, 4);
                        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= key[i];
                        return new IPAddress(bytes);
                    }
                    plain ??= new IPAddress(bytes);
                }
            }
            at = value + (size + 3 & ~3);
        }
        return plain;
    }
}
