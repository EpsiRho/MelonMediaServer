using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace MelonWebApi.Services
{
    /// <summary>
    /// Masks secret query values in log properties. ASP.NET logs every request URL, and clients put the media token
    /// (?jwt=) in stream and art URLs; old clients still send ?password= to removed sign-in endpoints.
    /// </summary>
    public sealed partial class LogRedaction : ILogEventEnricher
    {
        [GeneratedRegex(@"(?<=[?&;](?:jwt|token|access_token|refresh_token|password|pass|pwd|code|secret|key|apikey|api_key|otp|pin)=)[^&\s#]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex Secret();

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            List<LogEventProperty>? changed = null;
            foreach (var (name, value) in logEvent.Properties)
                if (value is ScalarValue { Value: string text } && text.Contains('='))
                {
                    var masked = Secret().Replace(text, "***");
                    if (masked != text)
                        (changed ??= []).Add(new LogEventProperty(name, new ScalarValue(masked)));
                }
            if (changed != null)
                foreach (var property in changed) logEvent.AddOrUpdateProperty(property);
        }
    }
}
