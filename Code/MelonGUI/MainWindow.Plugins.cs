using System.Text.Json;
using Melon.Interface;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private async Task PluginsPage(StackPanel page)
    {
        var plugins = await Read<PluginInfo[]>(nameof(IMelonAdministration.GetPlugins));
        var selected = plugins.FirstOrDefault(p => p.Name == selectedPlugin && p.Authors == selectedAuthor);
        if (selected != null)
        {
            page.Children.Add(ButtonFor("All plugins", () => { selectedPlugin = selectedAuthor = null; return Task.CompletedTask; }));
            var toggle = new ToggleSwitch { IsOn = selected.IsEnabled, OnContent = T("Enabled"), OffContent = T("Disabled") };
            toggle.Toggled += async (_, _) => await Act(async () =>
            {
                await Command(nameof(IMelonAdministration.SetPluginEnabled), new { name = selected.Name, author = selected.Authors, enabled = toggle.IsOn });
            });
            page.Children.Add(Card(Stack(Row(Text(selected.Name, "SubtitleTextBlockStyle"), toggle),
                Text(selected.Version + " · " + selected.Authors), Text(selected.Description))));
            if (selected.IsEnabled) await PluginMenu(page, selected);
            return;
        }
        var sort = Choices("Sort by", "Name", "Enabled first", "Disabled first");
        sort.SelectedIndex = pluginOrder == "Name" ? 0 : pluginOrder == "Enabled first" ? 1 : 2;
        sort.SelectionChanged += async (_, _) => { pluginOrder = Choice(sort); await Render(); };
        page.Children.Add(sort);
        var sorted = plugins.OrderBy(p => pluginOrder == "Enabled first" ? !p.IsEnabled : pluginOrder == "Disabled first" && p.IsEnabled)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var plugin in sorted)
        {
            var line = Row(Stack(Text(plugin.Name, "BodyStrongTextBlockStyle"), Text(plugin.Version, "CaptionTextBlockStyle")),
                Text(T(plugin.IsEnabled ? "Enabled" : "Disabled") + "    ›"));
            var button = ButtonFor(plugin.Name, () =>
            {
                selectedPlugin = plugin.Name; selectedAuthor = plugin.Authors; return Task.CompletedTask;
            });
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{plugin.Name} {plugin.Version}");
            button.Content = line;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(16,10,16,10);
            page.Children.Add(button);
        }
        if (plugins.Length == 0) page.Children.Add(Card(Text(T("No plugins installed."))));
        Section(page, "Manage plugins");
        page.Children.Add(Card(Stack(Row(Text(T("Plugin folder")), ButtonFor("Open folder", () => DesktopHost.OpenFolder(Path.Combine(status!.DataDirectory, "Plugins")))),
            Row(ButtonFor("Rescan folder", () => Command(nameof(IMelonAdministration.ReloadPlugins), new { rescan = true })),
                ButtonFor("Reload plugins", () => Command(nameof(IMelonAdministration.ReloadPlugins), new { rescan = false }))))));
    }

    private async Task PluginMenu(StackPanel page, PluginInfo plugin)
    {
        PluginPage menu;
        try { menu = await Read<PluginPage>(nameof(IMelonAdministration.GetPluginPage), new { name = plugin.Name, author = plugin.Authors }); }
        catch (InvalidOperationException ex) when (ex.Message == "Plugin has no active controller page.") { return; }
        var values = new Dictionary<string, Func<object?>>();
        var fields = Stack();
        foreach (var field in menu.Fields)
        {
            var value = (JsonElement)field.Value;
            switch (field.Type)
            {
                case "boolean":
                    var toggle = new CheckBox { Content = field.Label, IsChecked = value.ValueKind == JsonValueKind.True };
                    fields.Children.Add(toggle); values[field.Key] = () => toggle.IsChecked == true;
                    break;
                case "argb":
                    uint argb = unchecked((uint)value.GetInt32());
                    var color = new ColorPicker { Color = Windows.UI.Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb),
                        IsAlphaEnabled = true, IsColorSpectrumVisible = false };
                    fields.Children.Add(new Expander { Header = field.Label, Content = color, HorizontalAlignment = HorizontalAlignment.Stretch });
                    values[field.Key] = () => unchecked((int)((uint)color.Color.A << 24 | (uint)color.Color.R << 16 | (uint)color.Color.G << 8 | color.Color.B));
                    break;
                case "number":
                    var number = new NumberBox { Header = field.Label, Value = value.GetDouble() };
                    fields.Children.Add(number); values[field.Key] = () => number.Value;
                    break;
                case "text":
                    var input = new TextBox { Header = field.Label, Text = value.GetString() ?? "" };
                    fields.Children.Add(input); values[field.Key] = () => input.Text;
                    break;
                default:
                    fields.Children.Add(Text(field.Label + " · " + value));
                    values[field.Key] = () => value;
                    break;
            }
        }
        foreach (var action in menu.Actions)
            fields.Children.Add(ButtonFor(action.Label, async () =>
            {
                var result = await Command(nameof(IMelonAdministration.InvokePlugin), new { name = plugin.Name, author = plugin.Authors,
                    action = action.Key, values = values.ToDictionary(pair => pair.Key, pair => pair.Value()) });
                if (result.ValueKind == JsonValueKind.String) Notice(result.GetString()!);
            }));
        if (fields.Children.Count > 0) page.Children.Add(Card(fields));
    }
}
