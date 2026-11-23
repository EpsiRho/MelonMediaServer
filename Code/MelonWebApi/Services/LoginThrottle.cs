using System.Collections.Concurrent;

namespace MelonWebApi.Services;

/// <summary>
/// Slows down password guessing for one account from one address. After <see cref="FreeFailures"/> wrong passwords,
/// each further attempt has to wait (15 s, doubling, at most 15 minutes) until a correct one clears the record.
/// Keyed by username and client address together, so a stranger can't lock the real owner out of their account;
/// the per-address rate limit on all auth endpoints bounds how many accounts one address can try.
/// </summary>
internal static class LoginThrottle
{
    private const int FreeFailures = 5;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(15), MaxDelay = TimeSpan.FromMinutes(15), Forget = TimeSpan.FromHours(1);
    private static readonly ConcurrentDictionary<string, (int Failures, DateTime LockedUntil, DateTime Last)> Entries = new();

    private static string Key(string? username, string? address) => $"{username?.ToLowerInvariant()}\n{address}";

    /// <summary>How long this caller must wait before trying this account again, or <see cref="TimeSpan.Zero"/>.</summary>
    public static TimeSpan RetryAfter(string? username, string? address)
    {
        if (!Entries.TryGetValue(Key(username, address), out var e)) return TimeSpan.Zero;
        var wait = e.LockedUntil - DateTime.UtcNow;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    public static void Failed(string? username, string? address)
    {
        var now = DateTime.UtcNow;
        Entries.AddOrUpdate(Key(username, address),
            _ => (1, DateTime.MinValue, now),
            (_, e) =>
            {
                int n = now - e.Last > Forget ? 1 : e.Failures + 1;
                var locked = n <= FreeFailures ? DateTime.MinValue
                    : now + TimeSpan.FromTicks(Math.Min(MaxDelay.Ticks, BaseDelay.Ticks << Math.Min(n - FreeFailures - 1, 10)));
                return (n, locked, now);
            });
        if (Entries.Count > 10_000) Prune(now);
    }

    public static void Succeeded(string? username, string? address) => Entries.TryRemove(Key(username, address), out _);

    private static void Prune(DateTime now)
    {
        foreach (var (key, e) in Entries)
            if (now - e.Last > Forget && e.LockedUntil < now) Entries.TryRemove(key, out _);
    }
}
