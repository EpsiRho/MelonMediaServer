using System.Globalization;
using Melon.Interface;
using Admin = Melon.Interface.IMelonAdministration;

namespace MelonCLI;

internal sealed partial class Controller
{
    private static readonly int[] StatsPeriods = [7, 14, 30, 90, 180, 365];
    private ListeningSummary? stats, statsCompare;
    private DateTime nextStatsRefresh, statsUpdated;
    private string statsUser = "", statsUsername = "All users";
    private int statsDays = 30;

    /// <summary>Totals for one run of days, used for the current and the previous period.</summary>
    private sealed record Totals(long Plays, long Skips, double Ms, int Active, int Days)
    {
        public static Totals Of(IReadOnlyCollection<ListeningDay> days) => new(days.Sum(d => d.Plays), days.Sum(d => d.Skips),
            days.Sum(d => d.Milliseconds), days.Count(d => d.Plays > 0), days.Count);
        public double SkipRate => Plays + Skips == 0 ? 0 : Skips / (double)(Plays + Skips);
        public double PerDay => Days == 0 ? 0 : Plays / (double)Days;
        public double AvgPlay => Plays == 0 ? 0 : Ms / Plays;
    }

    private async Task LoadStats()
    {
        stats = await Read<ListeningSummary>(nameof(Admin.GetListeningSummary), new { userId = statsUser, days = statsDays });
        // The previous period comes from a double-length window; the server caps windows at 366 days.
        statsCompare = statsDays * 2 <= 366
            ? await Read<ListeningSummary>(nameof(Admin.GetListeningSummary), new { userId = statsUser, days = statsDays * 2 })
            : null;
        statsUpdated = DateTime.Now;
        nextStatsRefresh = DateTime.UtcNow.AddSeconds(20);
    }

    private string PeriodName(int days) => T($"{days} days");

    private (string? Text, Tone Tone) Delta(double current, double? previous, bool higherIsBetter, bool points = false)
    {
        if (previous is not double before) return (null, Tone.Muted);
        string vs = T("vs previous");
        if (points)
        {
            double diff = (current - before) * 100;
            if (Math.Abs(diff) < 0.05) return ($"= {vs}", Tone.Muted);
            bool good = diff > 0 == higherIsBetter;
            return ($"{(diff > 0 ? "▲" : "▼")} {Math.Abs(diff):0.0} {T("pts")} {vs}", good ? Tone.Good : Tone.Danger);
        }
        if (before == 0) return current == 0 ? (null, Tone.Muted) : ($"▲ {T("new")} {vs}", higherIsBetter ? Tone.Good : Tone.Danger);
        double change = (current - before) / before;
        if (Math.Abs(change) < 0.005) return ($"= {vs}", Tone.Muted);
        bool better = change > 0 == higherIsBetter;
        return ($"{(change > 0 ? "▲" : "▼")} {Math.Abs(change):P0} {vs}", better ? Tone.Good : Tone.Danger);
    }

    private static (int Length, DateTime Start, DateTime End) LongestRun(ListeningDay[] days, bool active)
    {
        int best = 0, run = 0; DateTime bestEnd = default, start = default, bestStart = default;
        foreach (var d in days)
        {
            if ((d.Plays > 0) == active) { if (run++ == 0) start = d.DateUtc; if (run > best) { best = run; bestStart = start; bestEnd = d.DateUtc; } }
            else run = 0;
        }
        return (best, bestStart, bestEnd);
    }

    private string DayCount(int n) => $"{n:N0} {T(n == 1 ? "day" : "days")}";

    private async Task Stats(Screen page)
    {
        if (!status!.DatabaseConnected) return;
        if (stats == null || DateTime.UtcNow >= nextStatsRefresh) await LoadStats();
        var summary = stats!;
        var days = summary.Days;
        var now = Totals.Of(days);
        var prevDays = statsCompare is { } compare && compare.Days.Length >= statsDays * 2 ? compare.Days[..statsDays] : null;
        var prev = prevDays == null ? null : Totals.Of(prevDays);
        var culture = CultureInfo.CurrentCulture;

        page.Body.Add(new Chips([
            (T("Range"), $"{days[0].DateUtc:MMM d} – {days[^1].DateUtc:MMM d} UTC"),
            (T("Updated"), statsUpdated.ToString("T", culture))]));

        // KPI tiles
        var plays = Delta(now.Plays, prev?.Plays, true);
        var time = Delta(now.Ms, prev?.Ms, true);
        var skips = Delta(now.Skips, prev?.Skips, false);
        var rate = Delta(now.SkipRate, prev?.SkipRate, false, points: true);
        var perDay = Delta(now.PerDay, prev?.PerDay, true);
        var active = Delta(now.Active, prev?.Active, true);
        var streak = LongestRun(days, true);
        int current = Enumerable.Reverse(days).TakeWhile(d => d.Plays > 0).Count();
        page.Body.Add(new Cards([
            new Card(T("Plays"), $"{now.Plays:N0}", plays.Text, plays.Tone, days.Select(d => (double)d.Plays).ToArray()),
            new Card(T("Listening time"), Fmt.Duration(now.Ms), time.Text, time.Tone, days.Select(d => d.Milliseconds).ToArray(), Tone.Info),
            new Card(T("Skips"), $"{now.Skips:N0}", skips.Text, skips.Tone, days.Select(d => (double)d.Skips).ToArray(), Tone.Flesh),
            new Card(T("Skip rate"), Fmt.Percent(now.SkipRate), rate.Text, rate.Tone,
                days.Select(d => d.Plays + d.Skips == 0 ? 0 : d.Skips / (double)(d.Plays + d.Skips)).ToArray(), Tone.Flesh),
            new Card(T("Daily average"), $"{now.PerDay:0.#} {T("plays")}", perDay.Text ?? $"{Fmt.Duration(now.Ms / Math.Max(1, now.Days))} / {T("day")}", perDay.Tone,
                days.Select(d => (double)d.Plays).ToArray()),
            new Card(T("Active days"), $"{now.Active:N0} / {now.Days:N0}", active.Text ?? $"{T("Streak")} {DayCount(current)}", active.Tone,
                days.Select(d => d.Plays > 0 ? 1d : 0d).ToArray())], 22));

        if (now.Plays + now.Skips == 0)
        {
            page.Body.Add(new Callout(T("Try a longer period or another user."), Tone.Info, T("No listening activity in this period.")));
        }
        else
        {
            // Activity chart: daily up to ~3 months, Monday-aligned weeks beyond that.
            bool weekly = statsDays > 120;
            var buckets = weekly
                ? days.GroupBy(d => d.DateUtc.Date.AddDays(-(((int)d.DateUtc.DayOfWeek + 6) % 7)))
                    .Select(g => new Bucket(g.Key.ToString("MMM d", culture), g.Sum(d => d.Plays), g.Sum(d => d.Skips), g.Sum(d => d.Milliseconds))).ToArray()
                : days.Select(d => new Bucket(d.DateUtc.ToString(statsDays <= 7 ? "ddd" : statsDays <= 14 ? "ddd d" : "MMM d", culture),
                    d.Plays, d.Skips, d.Milliseconds)).ToArray();
            var peak = buckets.MaxBy(b => b.A + b.B)!;
            page.Body.Add(new Panel(T("Listening activity"),
                new ColumnChart(buckets, 10, T("Plays"), T("Skips"), T("Time"), $"{T("Peak")} {peak.Label} · {peak.A + peak.B:N0}",
                    k => string.Format(culture, T(weekly ? "Bars average {0} weeks" : "Bars average {0} days"), k)),
                weekly ? T("per week") : T("per day"), Tone.Faint));

            // Top tracks and weekday rhythm
            Block top = summary.TopTracks.Length == 0
                ? new Para(T("No plays yet."), Tone.Muted)
                : new HBars(summary.TopTracks.Select((t, i) => new HBar(t.Name, t.Plays,
                    $"{t.Plays:N0}  {(now.Plays == 0 ? 0 : t.Plays / (double)now.Plays),4:P0}", Tone.Accent, $"{i + 1}", i == 0)));
            var names = culture.DateTimeFormat.AbbreviatedDayNames;
            var byWeekday = Enumerable.Range(0, 7).Select(i => (DayOfWeek)((i + 1) % 7))
                .Select(dow => (Day: dow, Plays: days.Where(d => d.DateUtc.DayOfWeek == dow).Sum(d => d.Plays),
                    Count: days.Count(d => d.DateUtc.DayOfWeek == dow))).ToArray();
            var busiest = byWeekday.MaxBy(w => w.Plays);
            var rhythm = new HBars(byWeekday.Select(w => new HBar(names[(int)w.Day], w.Plays,
                $"{w.Plays:N0}  {(w.Count == 0 ? 0 : w.Plays / (double)w.Count),5:0.0}/d", Tone.Accent, null, w.Day == busiest.Day && busiest.Plays > 0)));
            page.Body.Add(new Columns(36,
                new Panel(T("Top tracks"), top, T("share of plays"), Tone.Faint),
                new Panel(T("By weekday"), rhythm, T("plays · avg"), Tone.Faint)));

            // Highlights and calendar
            var best = days.MaxBy(d => d.Plays)!;
            var gap = LongestRun(days, false);
            var facts = new List<(string, string, Tone)>
            {
                (T("Best day"), $"{best.DateUtc.ToString("ddd, MMM d", culture)} · {best.Plays:N0} {T("plays")}", Tone.Normal),
                (T("Longest streak"), streak.Length == 0 ? "—" : $"{DayCount(streak.Length)}  ({streak.Start.ToString("MMM d", culture)} – {streak.End.ToString("MMM d", culture)})", Tone.Normal),
                (T("Current streak"), current == 0 ? "—" : DayCount(current), Tone.Normal),
                (T("Longest break"), gap.Length == 0 ? "—" : DayCount(gap.Length), Tone.Normal),
                (T("Busiest weekday"), busiest.Plays == 0 ? "—" : $"{culture.DateTimeFormat.GetDayName(busiest.Day)} · {busiest.Plays / (double)Math.Max(1, now.Plays):P0} {T("of plays")}", Tone.Normal),
                (T("Average play"), now.Plays == 0 ? "—" : Fmt.Duration(now.AvgPlay, seconds: true), Tone.Normal),
                (T("Plays per active day"), now.Active == 0 ? "—" : $"{now.Plays / (double)now.Active:0.#}", Tone.Normal),
            };
            if (prev != null) facts.Add((T("Previous period"), $"{prev.Plays:N0} {T("plays")} · {Fmt.Duration(prev.Ms)}", Tone.Normal));
            var highlights = new Panel(T("Highlights"), new KeyValues(facts));
            if (statsDays >= 28)
            {
                var calendar = new Panel(T("Calendar"), new Heatmap(days.Select(d => (d.DateUtc, (double)d.Plays)).ToArray(), T("Less"), T("More"),
                    $"{now.Active:N0} {T("active days")}"), "UTC", Tone.Faint);
                int weeks = (statsDays + 6) / 7 + 1;
                page.Body.Add(new Split(highlights, calendar, Math.Max(40, 4 + weeks * 2) + 4, 44));
            }
            else page.Body.Add(highlights);
        }

        // Actions
        int index = Array.IndexOf(StatsPeriods, statsDays);
        Entry Period(int step) => Local(PeriodName(StatsPeriods[Math.Clamp(index + step, 0, StatsPeriods.Length - 1)]), () =>
        {
            int next = StatsPeriods[Math.Clamp(index + step, 0, StatsPeriods.Length - 1)];
            if (next != statsDays) { statsDays = next; stats = null; }
        });
        var userEntry = new Entry(T("User"), async () =>
        {
            var users = await Read<ManagedUser[]>(nameof(Admin.GetUsers));
            var chosen = await Pick("User", users.Prepend(new ManagedUser("", T("All users"), "")), u => u.Username, u => u.Id == statsUser);
            statsUser = chosen.Id; statsUsername = chosen.Username; stats = null;
        }) { Value = statsUser == "" ? T("All users") : statsUsername };
        var refresh = Local(T("Refresh"), () => stats = null);
        page.Items.Add(Entry.Header(T("Filters")));
        page.Items.Add(userEntry);
        page.Items.Add(new Entry(T("Period"), async () =>
        {
            statsDays = await Pick("Period", StatsPeriods, PeriodName, n => n == statsDays); stats = null;
        }) { Value = PeriodName(statsDays) });
        page.Items.Add(Entry.Header(T("Explore")));
        page.Items.Add(Action("Daily breakdown", () => DailyBreakdown(summary)));
        page.Items.Add(refresh);
        page.Items.Add(Entry.Header(T("Maintenance")));
        page.Items.Add(Action("Prune deleted users’ stats", async () =>
        {
            await Confirm("Prune stats?", T("Permanently remove play history, play counts, skips, and ratings for deleted accounts?"), true);
            await Write(nameof(Admin.PruneDeletedUserStats)); stats = null; Say(T("Stats pruned."));
        }, Tone.Danger));
        page.Shortcuts.Add(new Shortcut(ConsoleKey.LeftArrow, "←→", T("Period"), Period(-1)));
        page.Shortcuts.Add(new Shortcut(ConsoleKey.RightArrow, "", "", Period(1)));
        page.Shortcuts.Add(new Shortcut(ConsoleKey.U, "U", T("User"), userEntry));
        page.Shortcuts.Add(new Shortcut(ConsoleKey.R, "R", T("Refresh"), refresh));
    }

    private async Task DailyBreakdown(ListeningSummary summary)
    {
        var culture = CultureInfo.CurrentCulture;
        var rows = Enumerable.Reverse(summary.Days).Select(d =>
        {
            long total = d.Plays + d.Skips;
            return (new[]
            {
                d.DateUtc.ToString("yyyy-MM-dd", culture), d.DateUtc.ToString("ddd", culture), $"{d.Plays:N0}", $"{d.Skips:N0}",
                total == 0 ? "—" : Fmt.Percent(d.Skips / (double)total), d.Milliseconds <= 0 ? "—" : Fmt.Duration(d.Milliseconds)
            }, (double)d.Plays, d.Plays == summary.Days.Max(x => x.Plays) && d.Plays > 0 ? Tone.Accent : Tone.Normal);
        });
        var totals = Totals.Of(summary.Days);
        await ShowBlocks("Daily breakdown",
            new Chips([(T("Plays"), $"{totals.Plays:N0}"), (T("Skips"), $"{totals.Skips:N0}"), (T("Listening time"), Fmt.Duration(totals.Ms)), (T("Active days"), $"{totals.Active:N0}")]),
            new Panel(T("Listening activity"), new Table([T("Date"), T("Day"), T("Plays"), T("Skips"), T("Skip rate"), T("Time")],
                [false, false, true, true, true, true], rows), "UTC", Tone.Faint));
    }
}
