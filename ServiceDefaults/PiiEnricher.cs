using Serilog.Core;
using Serilog.Events;

namespace ServiceDefaults;

/// <summary>
/// Serilog enricher that automatically redacts PII from log properties.
/// Looks for common property names like "name", "filePath", "audienceId" and redacts them.
/// </summary>
public class PiiEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToRedact = new[]
        {
            "name", "playerName", "audienceName", "userName",
            "filePath", "file", "path", "url", "fileUrl",
            "audienceId", "connectionId" // We'll keep connectionId but could redact audience names
        };

        foreach (var prop in logEvent.Properties.ToList())
        {
            var propName = prop.Key.ToLowerInvariant();
            
            // Check if property name suggests PII
            if (propertiesToRedact.Any(p => propName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                var originalValue = prop.Value.ToString("l", null);
                
                // Remove quotes that Serilog adds
                var cleanValue = originalValue.Trim('"');

                string redacted;
                if (propName.Contains("name", StringComparison.OrdinalIgnoreCase))
                {
                    redacted = PiiRedactor.RedactPlayerName(cleanValue);
                }
                else if (propName.Contains("path", StringComparison.OrdinalIgnoreCase) || 
                         propName.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                         propName.Contains("url", StringComparison.OrdinalIgnoreCase))
                {
                    redacted = PiiRedactor.RedactFilePath(cleanValue);
                }
                else if (propName.Contains("audience", StringComparison.OrdinalIgnoreCase))
                {
                    redacted = PiiRedactor.RedactAudienceId(cleanValue);
                }
                else
                {
                    // Default: hash it
                    redacted = PiiRedactor.RedactAudienceId(cleanValue);
                }

                // Replace the property with redacted value
                logEvent.RemovePropertyIfPresent(prop.Key);
                var redactedProperty = propertyFactory.CreateProperty(prop.Key, redacted);
                logEvent.AddPropertyIfAbsent(redactedProperty);
            }
        }
    }
}

