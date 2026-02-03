namespace Switchback.Core.Entities;

/// <summary>
/// Builds Activity RowKey for newest-first ordering in Table Storage. RowKey = reverse timestamp + messageId.
/// </summary>
public static class ActivityRowKeyHelper
{
    /// <summary>RowKey so that ascending sort gives newest first. Format: inverted ticks + "_" + messageId (sanitized).</summary>
    public static string ToRowKey(DateTimeOffset processedAt, string messageId)
    {
        long inverted = DateTime.MaxValue.Ticks - processedAt.UtcTicks;
        string safe = string.Join("_", (messageId ?? "").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return $"{inverted:D20}_{safe}";
    }
}
