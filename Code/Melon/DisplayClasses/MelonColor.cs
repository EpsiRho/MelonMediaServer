using Melon.LocalClasses;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace Melon.DisplayClasses
{
    /// <summary>
    /// The theme colors for melon's UI. Adjustable in the settings.
    /// </summary>
    public struct MelonColor
    {
        public static Color Text { get; set; }
        public static Color ShadedText { get; set; }
        public static Color BackgroundText { get; set; }
        public static Color Highlight { get; set; }
        public static Color Melon { get; set; }
        public static Color Error { get; set; }
        public static void SetDefaults()
        {
            Text = Color.FromArgb(204, 204, 204);
            ShadedText = Color.FromArgb(100,100,100);
            BackgroundText = Color.FromArgb(66, 66, 66);
            Highlight = Color.FromArgb(97, 214, 214);
            Melon = Color.FromArgb(26, 225, 19);
            Error = Color.FromArgb(255, 0, 0);
            try
            {
                StateManager.MelonSettings.Text = Color.FromArgb(204, 204, 204);
                StateManager.MelonSettings.ShadedText = Color.FromArgb(100, 100, 100);
                StateManager.MelonSettings.BackgroundText = Color.FromArgb(66, 66, 66);
                StateManager.MelonSettings.Highlight = Color.FromArgb(97, 214, 214);
                StateManager.MelonSettings.Melon = Color.FromArgb(26, 225, 19);
                StateManager.MelonSettings.Error = Color.FromArgb(255, 0, 0);
            }
            catch (Exception)
            {

            }
        }

    }
}
