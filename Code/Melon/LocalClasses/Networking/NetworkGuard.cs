using System.Collections.Concurrent;
using System.Net;
using Melon.Interface;
using Melon.Models;

namespace Melon.LocalClasses.Networking;

/// <summary>
/// The blocklist, a tally of who reaches Melon from outside the home network, and suggestions of addresses to block, drawn
/// from that tally and the security log.
/// </summary>
public static class NetworkGuard
{
    private sealed class Traffic
    {
        public DateTime First, Last, LastLoggedUtc, LastBlockedLogUtc;
        public long Requests, Denied, NotFound;
        public string? Username;
    }

    private static readonly ConcurrentDictionary<string, Traffic> Seen = new();
    private static readonly ConcurrentDictionary<string, DateTime> Dismissed = new();
    private static List<BlockedAddress>? blockSource;
    private static (IPAddress Network, int Prefix, string Text)[] blocks = [];
    private const int MaxTracked = 5000;

    private static (IPAddress Network, int Prefix, string Text)[] Blocks()
    {
        var current = StateManager.MelonSettings?.BlockedAddresses;
        if (!ReferenceEquals(current, blockSource))
        {
            blockSource = current;
            blocks = (current ?? []).Select(b => NetAddresses.TryParseBlock(b.Address, out var n, out var p) ? (n, p, b.Address) : (IPAddress.None, -1, b.Address))
                .Where(b => b.Item2 >= 0).ToArray();
        }
        return blocks;
    }

    public static bool IsBlocked(IPAddress? address) =>
        address != null && Blocks().Any(b => NetAddresses.Matches(address, b.Network, b.Prefix));

    /// <summary>A blocked address tried again: logged at most once an hour per address.</summary>
    public static void NoteBlocked(IPAddress address)
    {
        var key = Key(address);
        var t = Seen.GetOrAdd(key, _ => new Traffic { First = DateTime.UtcNow });
        lock (t)
        {
            t.Last = DateTime.UtcNow; t.Requests++; t.Denied++;
            if (DateTime.UtcNow - t.LastBlockedLogUtc < TimeSpan.FromHours(1)) return;
            t.LastBlockedLogUtc = DateTime.UtcNow;
        }
        Background(() => SecurityLog.Record(SecurityEventKinds.BlockedRequest, address: key));
    }

    private static readonly ConcurrentDictionary<string, DateTime> RefusedLogged = new();
    /// <summary>An outside request refused because Remote Access is off, or it came over plain HTTP. Logged at most once an hour per address.</summary>
    public static void NoteRefused(IPAddress address, string why)
    {
        var key = Key(address);
        Note(address, 403, null);
        var now = DateTime.UtcNow;
        if (RefusedLogged.TryGetValue(key, out var last) && now - last < TimeSpan.FromHours(1)) return;
        RefusedLogged[key] = now;
        Background(() => SecurityLog.Record(SecurityEventKinds.OutsideRequestRefused, address: key, detail: why));
    }

    /// <summary>Counts a finished request from outside the home network. The first one each day is written to the security log.</summary>
    public static void Note(IPAddress address, int status, string? username)
    {
        if (Seen.Count >= MaxTracked && !Seen.ContainsKey(Key(address)))
            foreach (var old in Seen.OrderBy(x => x.Value.Last).Take(MaxTracked / 10).Select(x => x.Key).ToArray()) Seen.TryRemove(old, out _);
        var key = Key(address);
        var t = Seen.GetOrAdd(key, _ => new Traffic { First = DateTime.UtcNow });
        bool log;
        lock (t)
        {
            t.Last = DateTime.UtcNow; t.Requests++;
            if (status is 401 or 403) t.Denied++;
            if (status == 404) t.NotFound++;
            if (!string.IsNullOrEmpty(username)) t.Username = username;
            log = status != 403 && DateTime.UtcNow - t.LastLoggedUtc > TimeSpan.FromDays(1);
            if (log) t.LastLoggedUtc = DateTime.UtcNow;
        }
        if (log) Background(() => SecurityLog.Record(SecurityEventKinds.RemoteClientSeen, username: username, address: key, detail: "First request today from outside your network"));
    }

    /// <summary>Writes to the security log off the request path, so a slow database can't hold up (or be flooded by) outside requests.</summary>
    private static void Background(Action record) => _ = Task.Run(() => { try { record(); } catch (Exception) { } });

    private static string Key(IPAddress a) => (a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a).ToString();

    public static RemoteClient[] Clients() => Seen
        .Select(x => { lock (x.Value) return new RemoteClient(x.Key, x.Value.First, x.Value.Last, x.Value.Requests, x.Value.Denied, x.Value.NotFound, x.Value.Username,
            IPAddress.TryParse(x.Key, out var ip) && IsBlocked(ip)); })
        .OrderByDescending(c => c.LastSeenUtc).Take(500).ToArray();

    public static void Dismiss(string address) => Dismissed[address.Trim()] = DateTime.UtcNow;

    /// <summary>Addresses worth blocking, worst first: repeated failed sign-ins, a reused sign-in token, being throttled, or poking at pages that don't exist.</summary>
    public static SecuritySuggestion[] Suggestions()
    {
        var found = new Dictionary<string, (int Score, string Reason, int Count, DateTime Last)>();
        void Add(string address, int score, string reason, int count, DateTime last)
        {
            if (!found.TryGetValue(address, out var e) || e.Score < score) found[address] = (score, reason, count, last > (e.Last) ? last : e.Last);
        }
        var events = SecurityLog.Recent(5000, sinceUtc: DateTime.UtcNow.AddDays(-7));
        foreach (var g in events.Where(e => e.Address is { Length: > 0 } a && a != "controller").GroupBy(e => e.Address))
        {
            var last = g.Max(e => e.TimeUtc);
            int reuse = g.Count(e => e.Kind == SecurityEventKinds.TokenReuse);
            int throttled = g.Count(e => e.Kind == SecurityEventKinds.SignInThrottled);
            int failed = g.Count(e => e.Kind is SecurityEventKinds.SignInFailed or SecurityEventKinds.PasswordChangeFailed);
            var users = g.Where(e => e.Kind == SecurityEventKinds.SignInFailed).Select(e => e.Username).Where(u => u.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (reuse > 0) Add(g.Key, 100, "A sign-in token from this address was used twice, which happens when one is copied.", reuse, last);
            if (failed >= 5 && users >= 3) Add(g.Key, 90, $"{failed} failed sign-ins for {users} different usernames: someone guessing accounts.", failed, last);
            else if (throttled > 0) Add(g.Key, 80, "Too many wrong passwords in a row; sign-in was paused for this address.", throttled, last);
            else if (failed >= 5) Add(g.Key, 60, $"{failed} failed sign-ins in the last week.", failed, last);
            int refused = g.Count(e => e.Kind == SecurityEventKinds.OutsideRequestRefused);
            if (refused > 0) Add(g.Key, 40, "Tried to reach Melon from outside while Remote Access was off.", refused, last);
        }
        foreach (var c in Clients())
        {
            if (c.Username == null && c.NotFound >= 20) Add(c.Address, 70, $"Asked for {c.NotFound:N0} pages that don't exist without signing in: looks like an internet scanner.", (int)Math.Min(int.MaxValue, c.NotFound), c.LastSeenUtc);
            else if (c.Username == null && c.Denied >= 15) Add(c.Address, 50, $"Refused {c.Denied:N0} times without ever signing in.", (int)Math.Min(int.MaxValue, c.Denied), c.LastSeenUtc);
        }
        return found
            .Where(x => IPAddress.TryParse(x.Key, out var ip) && !IsBlocked(ip) && NetAddresses.Scope(ip) != AddressScope.Loopback)
            .Where(x => !Dismissed.TryGetValue(x.Key, out var when) || x.Value.Last > when)
            .OrderByDescending(x => x.Value.Score).ThenByDescending(x => x.Value.Last)
            .Select(x => new SecuritySuggestion(x.Key, x.Value.Reason, x.Value.Count, x.Value.Last,
                NetAddresses.Scope(IPAddress.Parse(x.Key)) == AddressScope.Local))
            .Take(50).ToArray();
    }
}
