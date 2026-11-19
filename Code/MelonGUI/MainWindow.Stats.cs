using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private int statsDays = 7;
    private string statsUser = "";

    private async Task StatsPage(StackPanel page)
    {
        if (status?.DatabaseConnected != true) return;
        var users = await Read<ManagedUser[]>(nameof(IMelonAdministration.GetUsers));
        if (!users.Any(u => u.Id == statsUser)) statsUser = "";
        var filter = new ComboBox { Header = T("User"), HorizontalAlignment = HorizontalAlignment.Stretch };
        filter.Items.Add(new ComboBoxItem { Content = T("All users"), Tag = "" });
        foreach (var user in users) filter.Items.Add(new ComboBoxItem { Content = user.Username, Tag = user.Id });
        filter.SelectedItem = filter.Items.Cast<ComboBoxItem>().First(i => (string)i.Tag == statsUser);
        var range = Choices("Period", "7 days", "30 days", "90 days");
        int[] windows = [7, 30, 90];
        range.SelectedIndex = Array.IndexOf(windows, statsDays);
        filter.SelectionChanged += async (_, _) => { if (filter.SelectedItem is ComboBoxItem item) { statsUser = (string)item.Tag; await Render(); } };
        range.SelectionChanged += async (_, _) => { if (range.SelectedIndex >= 0) { statsDays = windows[range.SelectedIndex]; await Render(); } };
        page.Children.Add(Row(filter, range));

        var summary = await Read<ListeningSummary>(nameof(IMelonAdministration.GetListeningSummary), new { userId = statsUser, days = statsDays });
        var kpis = new Grid { ColumnSpacing = 8 };
        var seconds = Math.Round(summary.Days.Sum(d => d.Milliseconds) / 1000);
        var minutes = Math.Floor(seconds / 60);
        foreach (var (label, value) in new[] { ("Plays", summary.Days.Sum(d => d.Plays).ToString("N0")), ("Skips", summary.Days.Sum(d => d.Skips).ToString("N0")),
            ("Listening time", minutes >= 60 ? $"{Math.Floor(minutes / 60):N0} h {minutes % 60:0} min" : minutes > 0 ? $"{minutes:N0} min" : $"{seconds:N0} s") })
        {
            kpis.ColumnDefinitions.Add(new());
            var tile = Card(Stack(Text(value, "SubtitleTextBlockStyle"), Text(T(label))));
            Grid.SetColumn(tile, kpis.Children.Count); kpis.Children.Add(tile);
        }
        page.Children.Add(kpis);
        Section(page, "Listening activity");
        if (summary.Days.Any(d => d.Plays + d.Skips > 0))
        {
            var buckets = summary.Days.Chunk(statsDays > 30 ? 7 : 1).ToArray();
            var peak = Math.Max(1, buckets.Max(b => b.Sum(d => d.Plays + d.Skips)));
            var chart = new Grid { Height = 120, ColumnSpacing = 2 };
            var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            Brush neutral = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 177, 109, 32));
            foreach (var bucket in buckets)
            {
                chart.ColumnDefinitions.Add(new());
                var plays = bucket.Sum(d => d.Plays);
                var skips = bucket.Sum(d => d.Skips);
                var bars = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
                bars.Children.Add(new Border { Background = neutral, Height = 120d * skips / peak });
                bars.Children.Add(new Border { Background = accent, Height = 120d * plays / peak });
                var hit = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                hit.Children.Add(bars);
                var dates = bucket[0].DateUtc.ToString("d") + (bucket.Length > 1 ? " – " + bucket[^1].DateUtc.ToString("d") : "");
                var label = $"{dates} · {plays:N0} {T("Plays")} · {skips:N0} {T("Skips")}";
                AutomationProperties.SetName(hit, label); ToolTipService.SetToolTip(hit, label);
                Grid.SetColumn(hit, chart.Children.Count); chart.Children.Add(hit);
            }
            var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var (name, brush) in new[] { ("Plays", accent), ("Skips", neutral) })
            {
                legend.Children.Add(new Border { Width = 10, Height = 10, Background = brush, CornerRadius = new(2) });
                legend.Children.Add(Text(T(name)));
            }
            page.Children.Add(Card(Stack(legend, chart, Row(Text(summary.Days[0].DateUtc.ToString("MMM d") + " UTC"), Text(summary.Days[^1].DateUtc.ToString("MMM d"))))));
        }
        else page.Children.Add(Card(Text(T("No listening activity in this period."))));
        if (summary.TopTracks.Length > 0)
        {
            Section(page, "Top tracks");
            var tracks = Stack();
            foreach (var track in summary.TopTracks)
            {
                tracks.Children.Add(Row(Text(track.Name), Text(track.Plays.ToString("N0"))));
                tracks.Children.Add(new ProgressBar { Maximum = summary.TopTracks[0].Plays, Value = track.Plays });
            }
            page.Children.Add(Card(tracks));
        }
        page.Children.Add(ButtonFor("Refresh", () => Task.CompletedTask));
        page.Children.Add(ButtonFor("Prune deleted users’ stats", async () =>
        {
            if (await Dialog("Prune stats?", Text(T("Permanently remove play history, play counts, skips, and ratings for deleted accounts?")), "Prune", true))
            {
                await Command(nameof(IMelonAdministration.PruneDeletedUserStats));
                Notice("Stats pruned.");
            }
        }));
    }
}
