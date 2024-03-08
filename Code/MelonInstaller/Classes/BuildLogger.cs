using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelonInstaller.Classes
{
    public class BuildLogger : ILogger
    {
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;
        public string Parameters { get; set; }

        // Event handler for build started
        public void Initialize(IEventSource eventSource)
        {
            eventSource.BuildStarted += (sender, e) =>
            {
                Console.Write($"[-] Build started at {e.Timestamp}");
            };

            eventSource.BuildFinished += (sender, e) =>
            {
                Console.WriteLine();
                Console.WriteLine($"[+] Build finished at {e.Timestamp}. Success: {e.Succeeded}");
            };

            // Adjust this to capture more specific progress details if needed
            eventSource.ProjectStarted += (sender, e) =>
            {
                Console.Write($".");
            };

            eventSource.ProjectFinished += (sender, e) =>
            {
                Console.Write($".");
            };

            eventSource.ErrorRaised += (sender, e) =>
            {
                Console.WriteLine();
                Console.WriteLine($"[!] Error: {e.Message}");
            };
        }

        public void Shutdown()
        {

        }
    }
}
