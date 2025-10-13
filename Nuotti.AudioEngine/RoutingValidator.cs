namespace Nuotti.AudioEngine;

public static class RoutingValidator
{
    public sealed record Result(bool IsValid, IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors);

    public static Result ValidateAgainstDeviceChannels(RoutingOptions routing, int deviceChannelCount)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        if (routing is null)
        {
            errors.Add("Routing is null");
            return new Result(false, warnings, errors);
        }

        // helper to validate an array
        void Check(string busName, int[]? channels)
        {
            if (channels is null)
            {
                errors.Add($"Routing.{busName} is null");
                return;
            }
            for (int i = 0; i < channels.Length; i++)
            {
                int ch = channels[i];
                if (ch <= 0)
                {
                    errors.Add($"Routing.{busName}[{i}] = {ch} must be 1-based positive index");
                }
                else if (ch > deviceChannelCount)
                {
                    errors.Add($"Routing.{busName}[{i}] = {ch} exceeds device channels ({deviceChannelCount})");
                }
            }
        }

        Check("Tracks", routing.Tracks);
        Check("Click", routing.Click);

        return new Result(errors.Count == 0, warnings, errors);
    }
}
