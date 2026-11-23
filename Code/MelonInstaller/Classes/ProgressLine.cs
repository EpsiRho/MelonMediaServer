using System.Diagnostics;

namespace MelonInstaller.Classes
{
    /// <summary>
    /// One line of progress that redraws itself in place, like "[2/5] CLI          14s" or "Extracting    63%".
    /// It shows the elapsed time until <see cref="Report"/> gives it a percentage. When output is redirected
    /// (CI, logs) it skips the redrawing and just prints the finished line.
    /// </summary>
    public sealed class ProgressLine : IDisposable
    {
        private static readonly bool Live = !Console.IsOutputRedirected;
        private readonly string label;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer? timer;
        private readonly object gate = new();
        private double? percent;
        private bool finished;

        public ProgressLine(string label)
        {
            this.label = label;
            if (Live) timer = new Timer(_ => Draw(), null, 0, 250);
        }

        public TimeSpan Elapsed => clock.Elapsed;

        /// <summary>Show a percentage (0 to 1) instead of the elapsed time.</summary>
        public void Report(double value) => percent = Math.Clamp(value, 0, 1);

        /// <summary>Draw the line one last time with <paramref name="status"/> (the elapsed time if null) and move to the next line.</summary>
        public void Finish(string? status = null)
        {
            timer?.Dispose();
            lock (gate)
            {
                if (finished) return;
                finished = true;
                status ??= Seconds(clock.Elapsed);
                if (Live) { Write(status); Console.WriteLine(); }
                else Console.WriteLine($"{label.PadRight(20)} {status}");
            }
        }

        public void Dispose() => Finish();

        private void Draw()
        {
            lock (gate)
            {
                if (finished) return;
                Write(percent is double p ? $"{p * 100,3:0}%" : Seconds(clock.Elapsed));
            }
        }

        private void Write(string status)
        {
            var line = $"{label.PadRight(20)} {status}";
            // Pad with spaces so a shorter line covers up whatever the last one left behind.
            Console.Write("\r" + line.PadRight(Math.Max(line.Length, Width() - 1)));
        }

        public static string Seconds(TimeSpan time) => time.TotalMinutes >= 1 ? $"{(int)time.TotalMinutes}m {time.Seconds:00}s" : $"{time.TotalSeconds:0}s";

        private static int Width()
        {
            try { return Console.WindowWidth; }
            catch (IOException) { return 80; }
        }
    }
}
