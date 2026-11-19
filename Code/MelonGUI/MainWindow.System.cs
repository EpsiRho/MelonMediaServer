using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private async Task SystemPage(StackPanel page)
    {
        page.Children.Add(Card(Row(Stack(Text("Melon Media Server", "SubtitleTextBlockStyle"),
            Text(status == null ? T(serviceRunning ? "Running" : "Offline") : string.Join(" · ", new[] { status.Version, status.Branch }.Where(s => !string.IsNullOrWhiteSpace(s))))),
            status == null && !serviceRunning ? ButtonFor("Start Melon", StartHost) : ButtonFor("Stop Melon", async () =>
            {
                if (status == null) await DesktopHost.StopService(lifetime.Token);
                else await Command(nameof(IMelonAdministration.Stop));
                stopRequested = true; serviceRunning = false;
                status = null; UpdateWarning(); await Render();
            }, refresh: false))));
        if (status != null)
        {
            page.Children.Add(Card(Row(Text(T("Melon folder")), ButtonFor("Open folder", () => DesktopHost.OpenFolder(status.DataDirectory)))));
            var codes = await Read<string[]>(nameof(IMelonAdministration.GetLanguages));
            var langs = new ComboBox();
            foreach (var code in codes)
            {
                var culture = System.Globalization.CultureInfo.GetCultureInfo(code);
                langs.Items.Add(new ComboBoxItem { Tag = code, Content = culture.NativeName });
            }
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(langs, T("Server language"));
            langs.SelectedIndex = Array.FindIndex(codes, c => c.Equals(language, StringComparison.OrdinalIgnoreCase));
            langs.SelectionChanged += async (_, _) =>
            {
                if (langs.SelectedItem is ComboBoxItem { Tag: string code })
                    await Act(() => Command(nameof(IMelonAdministration.SetDefaultLanguage), new { language = code }));
            };
            page.Children.Add(Card(Row(Text(T("Server language")), langs)));
        }
        var theme = Choices("", "Windows default", "Light", "Dark");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(theme, T("Controller theme"));
        theme.SelectedIndex = Root.RequestedTheme == ElementTheme.Light ? 1 : Root.RequestedTheme == ElementTheme.Dark ? 2 : 0;
        theme.SelectionChanged += (_, _) =>
        {
            try
            {
                Root.RequestedTheme = theme.SelectedIndex == 1 ? ElementTheme.Light : theme.SelectedIndex == 2 ? ElementTheme.Dark : ElementTheme.Default;
                DesktopHost.SaveTheme(Root.RequestedTheme);
            }
            catch (Exception ex) { Error(ex); }
        };
        page.Children.Add(Card(Row(Text(T("Controller theme")), theme)));
        if (status == null) return;
        Section(page, "Updates");
        page.Children.Add(Card(Row(Text(release == null ? T("Melon updates") : release.UpdateAvailable ? T("Update available") + " · " + release.LatestVersion : T("Up to date")),
            ButtonFor("Check for updates", async () => release = await Read<ReleaseInfo>(nameof(IMelonAdministration.CheckForUpdates))))));
        if (release?.UpdateAvailable == true)
            page.Children.Add(ButtonFor("Install update", async () =>
            {
                if (await Read<bool>(nameof(IMelonAdministration.ApplyUpdate))) Close();
            }, true, false));
        if (release != null)
        {
            Section(page, "Latest changelog");
            var notes = Text(release.ReleaseNotes.Length == 0 ? T("No release notes.") : release.ReleaseNotes);
            notes.IsTextSelectionEnabled = true;
            page.Children.Add(Card(Stack(Text(release.LatestVersion, "BodyStrongTextBlockStyle"), notes)));
        }
        else page.Children.Add(ButtonFor("Latest changelog", async () => release = await Read<ReleaseInfo>(nameof(IMelonAdministration.CheckForUpdates))));
    }
}
