using System.Text;

namespace Nuotti.Contracts.V1.Governance;

public sealed record SupportEvidenceItem(string Name, string Content);

/// <summary>
/// Builds bounded support-bundle evidence with redacted identifiers and a hard size cap.
/// </summary>
public sealed class BoundedSupportEvidence
{
    public const int DefaultMaxItems = 32;
    public const int DefaultMaxCharsPerItem = 4_000;
    public const int DefaultMaxTotalChars = 48_000;

    readonly List<SupportEvidenceItem> _items = [];
    readonly int _maxItems;
    readonly int _maxCharsPerItem;
    readonly int _maxTotalChars;
    int _totalChars;

    public BoundedSupportEvidence(
        int maxItems = DefaultMaxItems,
        int maxCharsPerItem = DefaultMaxCharsPerItem,
        int maxTotalChars = DefaultMaxTotalChars)
    {
        _maxItems = maxItems;
        _maxCharsPerItem = maxCharsPerItem;
        _maxTotalChars = maxTotalChars;
    }

    public IReadOnlyList<SupportEvidenceItem> Items => _items;
    public bool Truncated { get; private set; }

    public bool TryAdd(string name, string content)
    {
        if (_items.Count >= _maxItems || _totalChars >= _maxTotalChars)
        {
            Truncated = true;
            return false;
        }

        var safeName = SafeTelemetryIdentifiers.RedactSecrets(name);
        var safeContent = SafeTelemetryIdentifiers.RedactSecrets(content);
        if (safeContent.Length > _maxCharsPerItem)
        {
            safeContent = safeContent[.._maxCharsPerItem] + "…[truncated]";
            Truncated = true;
        }

        var remaining = _maxTotalChars - _totalChars;
        if (safeContent.Length > remaining)
        {
            safeContent = safeContent[..Math.Max(0, remaining)] + "…[truncated]";
            Truncated = true;
        }

        _items.Add(new SupportEvidenceItem(safeName, safeContent));
        _totalChars += safeContent.Length;
        return true;
    }

    public string RenderManifest(string correlationId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"correlation={correlationId}");
        sb.AppendLine($"items={_items.Count}");
        sb.AppendLine($"truncated={Truncated}");
        foreach (var item in _items)
            sb.AppendLine($"- {item.Name} ({item.Content.Length} chars)");
        return sb.ToString();
    }
}
