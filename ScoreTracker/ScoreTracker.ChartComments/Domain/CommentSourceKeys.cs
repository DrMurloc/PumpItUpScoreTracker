namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     This vertical's source keys on the translation pipeline. The pipeline treats them as
///     opaque; only this class reads one back, so the format lives in exactly one place.
/// </summary>
internal static class CommentSourceKeys
{
    private const string Prefix = "chart-comment:";

    public static string For(Guid commentId)
    {
        return $"{Prefix}{commentId:N}";
    }

    /// <summary>Null for a key some other text owner minted — not ours to act on.</summary>
    public static Guid? TryParse(string sourceKey)
    {
        if (!sourceKey.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        return Guid.TryParseExact(sourceKey[Prefix.Length..], "N", out var id) ? id : null;
    }
}
