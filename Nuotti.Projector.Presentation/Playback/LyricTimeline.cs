using System.Globalization;
using System.Text.RegularExpressions;

namespace Nuotti.Projector.Presentation.Playback;

public sealed record LyricLine(TimeSpan Timestamp, string Text);

public sealed partial class LyricTimeline
{
    readonly IReadOnlyList<LyricLine> _lines;

    LyricTimeline(IReadOnlyList<LyricLine> lines) => _lines = lines;

    public static LyricTimeline Parse(string lrc)
    {
        ArgumentNullException.ThrowIfNull(lrc);
        var lines = new List<LyricLine>();

        foreach (var sourceLine in lrc.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = TimestampedLine().Match(sourceLine.Trim());
            if (!match.Success)
            {
                continue;
            }

            var minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
            var seconds = double.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture);
            lines.Add(new LyricLine(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds), match.Groups["text"].Value));
        }

        return new LyricTimeline(lines.OrderBy(line => line.Timestamp).ToArray());
    }

    public LyricLine? ActiveLineAt(TimeSpan playbackPosition, TimeSpan songStartOffset)
    {
        var lyricPosition = playbackPosition - songStartOffset;
        if (lyricPosition < TimeSpan.Zero)
        {
            return null;
        }

        LyricLine? active = null;
        foreach (var line in _lines)
        {
            if (line.Timestamp > lyricPosition)
            {
                break;
            }

            active = line;
        }

        return active;
    }

    [GeneratedRegex(@"^\[(?<minutes>\d{1,3}):(?<seconds>\d{2}(?:\.\d{1,3})?)\](?<text>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampedLine();
}
