using Melon.Interface;
using Microsoft.UI.Xaml.Controls;

namespace MelonGUI;

public sealed partial class MainWindow
{
    private void NetworkingPage(StackPanel page)
    {
        var config = settings!;
        var url = new TextBox { Header = T("Listening URL"), Text = config.ListeningUrl };
        page.Children.Add(Card(Row(url, ButtonFor("Save", async () =>
            await Command(nameof(IMelonAdministration.SetListeningUrl), new { url = url.Text.Trim() })))));

        Section(page, "HTTPS");
        var cert = new TextBox { Header = T("Certificate (.pfx)"), Text = config.CertificatePath ?? "" };
        var browse = new Button { Content = T("Browse") };
        browse.Click += async (_, _) => { try { if (await PickFile(".pfx", ".p12") is string file) cert.Text = file; } catch (Exception ex) { Error(ex); } };
        var password = new PasswordBox { Header = T("Certificate password") };
        var current = config.ListeningUrl.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:");
        var port = new NumberBox { Header = T("HTTPS port"), Minimum = 1, Maximum = 65535,
            Value = Uri.TryCreate(current, UriKind.Absolute, out var uri) ? uri.Port : 443 };
        var apply = ButtonFor("Apply HTTPS", async () =>
        {
            if (port.Value != Math.Truncate(port.Value) || !(port.Value >= 1 && port.Value <= 65535))
                throw new ArgumentException(T("Enter a valid port."));
            var listen = WithPort(url.Text.Trim(), "https", (int)port.Value);
            if (cert.Text != config.CertificatePath || password.Password.Length > 0 || string.IsNullOrEmpty(config.CertificatePath))
                await Command(nameof(IMelonAdministration.SetCertificate), new { path = cert.Text.Trim(), password = password.Password });
            await Command(nameof(IMelonAdministration.SetListeningUrl), new { url = listen });
        });
        var disable = ButtonFor("Disable HTTPS", async () =>
        {
            if (!await Dialog("Disable HTTPS?", Text(T("Connections will use HTTP.")), "Disable HTTPS", true)) return;
            var listen = WithPort(config.ListeningUrl, "http", (int)port.Value);
            await Command(nameof(IMelonAdministration.SetListeningUrl), new { url = listen });
            await Command(nameof(IMelonAdministration.DisableCertificate));
        });
        disable.IsEnabled = config.CertificatePath?.Length > 0 || config.ListeningUrl.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
        page.Children.Add(Card(Stack(Row(cert, browse), password, port, Row(apply, disable))));
    }

    private static string WithPort(string value, string scheme, int port)
    {
        var normalized = value.Replace("://*:", "://localhost:").Replace("://+:", "://localhost:");
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.PathAndQuery != "/" || uri.Fragment.Length != 0) throw new ArgumentException("Invalid listening URL.");
        var host = value.Contains("://*:") ? "*" : value.Contains("://+:") ? "+" : uri.Host;
        return $"{scheme}://{host}:{port}";
    }
}
