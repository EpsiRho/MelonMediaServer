using Melon.LocalClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Melon.DisplayClasses
{
    public static class NotificationManager
    {
        public static void ShowToast(string title, string message)
        {
            // Get the app image
            Directory.CreateDirectory($"{StateManager.melonPath}/Assets");
            string imageSrc = $"{StateManager.melonPath}/Assets/AppIcon.png";

            try 
            { 
                using var iconStream = TrayIconManager.GetStream();
                Bitmap b = (Bitmap)Bitmap.FromStream(new FileStream(imageSrc, FileMode.CreateNew));

                using (MemoryStream ms = new MemoryStream())
                {
                    b.Save(ms, ImageFormat.Png);
                }
            }
            catch (Exception)
            {

            }


            // PowerShell command to create and show a toast notification
            string psCommand = @$"$ToastTitle = '{title}'
$ToastText = '{message}'
$imageSrc = '{imageSrc}'
$imagePlacement = 'appLogoOverride'

[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null
$Template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastImageAndText02)

$RawXml = [xml] $Template.GetXml()
($RawXml.toast.visual.binding.text|where {{$_.id -eq '1'}}).AppendChild($RawXml.CreateTextNode($ToastTitle)) > $null
($RawXml.toast.visual.binding.text|where {{$_.id -eq '2'}}).AppendChild($RawXml.CreateTextNode($ToastText)) > $null
($RawXml.toast.visual.binding.image|where {{$_.id -eq '1'}}).SetAttribute('src', $imageSrc) > $null
($RawXml.toast.visual.binding.image|where {{$_.id -eq '1'}}).SetAttribute('placement', $imagePlacement) > $null
#($RawXml.toast.visual.binding.image|where {{$_.id -eq '1'}}).SetAttribute('hint-crop', 'circle') > $null

$SerializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
$SerializedXml.LoadXml($RawXml.OuterXml)

$Toast = [Windows.UI.Notifications.ToastNotification]::new($SerializedXml)
$Toast.Tag = 'Melon Media Server'
$Toast.Group = 'Melon Media Server'
$Toast.ExpirationTime = [DateTimeOffset]::Now.AddMinutes(1)

$Notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Melon Media Server')
$Notifier.Show($Toast)";

            // Execute the PowerShell command
            var process = new Process();
                process.StartInfo.FileName = "powershell";
                process.StartInfo.Arguments = $"-Command \"{psCommand}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.OutputDataReceived += (obj, args) => 
                {
                    Console.WriteLine($"{args.Data}");
                };
                process.Start();

                process.BeginOutputReadLine();
        }
    }
}
