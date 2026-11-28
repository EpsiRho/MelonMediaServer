using System.Buffers.Binary;
using System.Net;
using System.Text;
using Makaretu.Dns;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Serilog;

namespace Melon.LocalClasses.Networking;

/// <summary>
/// Pkarr (https://pkarr.org): signed DNS records stored in the BitTorrent Mainline DHT under an Ed25519 public key. Melon
/// publishes its current addresses there through free public relays, so a client that only knows the key can find the
/// server after its home IP changes. Nobody needs an account, and Melon runs no server for it.
/// <para>Records (names relative to the key): TXT "_melon" with "id=…", "fp=…" and one "u=URL" per address.</para>
/// </summary>
public static class Pkarr
{
    private const string Alphabet = "ybndrfg8ejkmcpqxot1uwisza345h769";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string ZBase32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = buffer << 8 | b; bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[buffer >> (bits - 5) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alphabet[buffer << (5 - bits) & 31]);
        return sb.ToString();
    }

    public static byte[]? FromZBase32(string text)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text.ToLowerInvariant())
        {
            int v = Alphabet.IndexOf(c);
            if (v < 0) return null;
            buffer = buffer << 5 | v; bits += 5;
            if (bits >= 8) { bytes.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        return [.. bytes];
    }

    /// <summary>BEP44's signed bytes: "3:seqi{seq}e1:v{len}:{v}".</summary>
    private static byte[] Signable(long seq, byte[] v) =>
        [.. Encoding.ASCII.GetBytes($"3:seqi{seq}e1:v{v.Length}:"), .. v];

    public static byte[] Packet(string serverId, string? fingerprint, IEnumerable<string> urls, string key)
    {
        var message = new Message { QR = true, AA = true };
        var strings = new List<string> { "v=1", "id=" + serverId };
        if (fingerprint != null) strings.Add("fp=" + fingerprint);
        foreach (var url in urls) strings.Add("u=" + url);
        message.Answers.Add(new TXTRecord { Name = "_melon." + key, Strings = strings, TTL = TimeSpan.FromMinutes(5) });
        var bytes = message.ToByteArray();
        if (bytes.Length > 1000) throw new InvalidOperationException("Too many addresses for a Pkarr record.");
        return bytes;
    }

    /// <summary>Signs and sends the record to each relay. Returns the relays that accepted it.</summary>
    public static async Task<string[]> Publish(Ed25519PrivateKeyParameters key, byte[] packet, IEnumerable<string> relays, CancellationToken token)
    {
        long seq = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        var signable = Signable(seq, packet);
        signer.BlockUpdate(signable, 0, signable.Length);
        var signature = signer.GenerateSignature();
        var body = new byte[64 + 8 + packet.Length];
        signature.CopyTo(body, 0);
        BinaryPrimitives.WriteInt64BigEndian(body.AsSpan(64), seq);
        packet.CopyTo(body, 72);
        var name = ZBase32(key.GeneratePublicKey().GetEncoded());
        var accepted = new List<string>();
        await Task.WhenAll(relays.Select(async relay =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, relay.TrimEnd('/') + "/" + name) { Content = new ByteArrayContent(body) };
                using var response = await Http.SendAsync(request, token);
                if (response.IsSuccessStatusCode) lock (accepted) accepted.Add(relay);
                else Log.Debug("Pkarr relay {Relay} refused the record: {Status}", relay, (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !token.IsCancellationRequested)
            { Log.Debug("Pkarr relay {Relay} unavailable: {Message}", relay, ex.Message); }
        }));
        return [.. accepted];
    }

    public sealed record Record(string? ServerId, string? Fingerprint, string[] Urls, DateTimeOffset Published);

    /// <summary>Looks the key up on the relays and verifies the signature. Used by the self-check; clients do the same.</summary>
    public static async Task<Record?> Resolve(string key, IEnumerable<string> relays, CancellationToken token)
    {
        var publicKey = FromZBase32(key);
        if (publicKey is not { Length: 32 }) return null;
        Record? best = null;
        foreach (var relay in relays)
        {
            try
            {
                var body = await Http.GetByteArrayAsync(relay.TrimEnd('/') + "/" + key, token);
                if (body.Length < 72) continue;
                long seq = BinaryPrimitives.ReadInt64BigEndian(body.AsSpan(64));
                var packet = body[72..];
                var verifier = new Ed25519Signer();
                verifier.Init(false, new Ed25519PublicKeyParameters(publicKey));
                var signable = Signable(seq, packet);
                verifier.BlockUpdate(signable, 0, signable.Length);
                if (!verifier.VerifySignature(body[..64])) continue;
                var message = new Message();
                message.Read(packet);
                string? id = null, fp = null; var urls = new List<string>();
                foreach (var txt in message.Answers.OfType<TXTRecord>().Where(r => r.Name.ToString().StartsWith("_melon", StringComparison.OrdinalIgnoreCase)))
                    foreach (var s in txt.Strings)
                    {
                        if (s.StartsWith("id=")) id = s[3..];
                        else if (s.StartsWith("fp=")) fp = s[3..];
                        else if (s.StartsWith("u=")) urls.Add(s[2..]);
                    }
                var found = new Record(id, fp, [.. urls], DateTimeOffset.FromUnixTimeMilliseconds(seq / 1000));
                if (best == null || found.Published > best.Published) best = found;
            }
            catch (Exception ex) when (ex is HttpRequestException or FormatException or ArgumentException or IOException || ex is TaskCanceledException && !token.IsCancellationRequested) { }
        }
        return best;
    }
}
