using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private static TextBlock Text(string value, string? style = null)
    {
        var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (style != null) text.Style = (Style)Application.Current.Resources[style];
        return text;
    }

    private static StackPanel Stack(params UIElement[] items)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var item in items) panel.Children.Add(item);
        return panel;
    }

    private static Grid Row(FrameworkElement label, FrameworkElement action)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(action, 1);
        row.Children.Add(label); row.Children.Add(action);
        return row;
    }

    private static Border Card(UIElement content) => new()
        { Style = (Style)Application.Current.Resources["ControlCard"], Child = content };

    private void Section(StackPanel page, string key) =>
        page.Children.Add(new TextBlock { Text = T(key), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            Margin = new Thickness(0,12,0,0) });

    private Button ButtonFor(string label, Func<Task> action, bool accent = false, bool refresh = true)
    {
        var button = new Button { Content = T(label), VerticalAlignment = VerticalAlignment.Center };
        if (accent) button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        button.Click += async (_, _) => await Act(action, refresh);
        return button;
    }

    private ComboBox Choices(string label, params string[] choices)
    {
        var control = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        if (label.Length > 0) control.Header = T(label);
        foreach (var key in choices) control.Items.Add(new ComboBoxItem { Content = T(key), Tag = key });
        control.SelectedIndex = 0;
        return control;
    }
    private static string Choice(ComboBox control) => (string)((ComboBoxItem)control.SelectedItem).Tag;

    private async Task<bool> Dialog(string title, UIElement content, string accept, bool destructive = false)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme, Title = T(title),
            Content = content, PrimaryButtonText = T(accept), CloseButtonText = T("Cancel"),
            DefaultButton = destructive ? ContentDialogButton.Close : ContentDialogButton.Primary };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<string?> PickFolder()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        return (await picker.PickSingleFolderAsync())?.Path;
    }
    private async Task<string?> PickFile(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        foreach (var extension in extensions) picker.FileTypeFilter.Add(extension);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        return (await picker.PickSingleFileAsync())?.Path;
    }

    private Button IconAction(string glyph, string label, Func<Task> action)
    {
        var button = ButtonFor(label, action);
        button.Content = new FontIcon { Glyph = glyph, FontSize = 14 };
        ToolTipService.SetToolTip(button, label);
        AutomationProperties.SetName(button, label);
        return button;
    }

    private async Task<string[]?> EditList(string title, IEnumerable<string> values)
    {
        var lines = new TextBox { Text = string.Join(Environment.NewLine, values), AcceptsReturn = true,
            MinHeight = 120, Header = T("One per line"), TextWrapping = TextWrapping.Wrap };
        if (!await Dialog(title, lines, "Save")) return null;
        return lines.Text.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
    }
}
