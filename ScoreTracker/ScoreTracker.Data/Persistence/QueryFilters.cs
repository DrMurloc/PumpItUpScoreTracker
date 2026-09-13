namespace ScoreTracker.Data.Persistence;

/// <summary>
///     The names of the global query filters the shared context carries, so a reader that must see
///     past one drops it by name and nothing else (docs/design/seasons.md D12).
/// </summary>
public static class QueryFilters
{
    /// <summary>
    ///     On every season-discriminated entity: <c>SeasonId == 0</c>. Every read sees all-time rows
    ///     unless it drops this filter; a seasonal reader drops it and applies its own season; the
    ///     account purge drops every filter so a deletion crosses seasons. Raw SQL never sees it and
    ///     spells <c>SeasonId = 0</c> by hand.
    /// </summary>
    public const string AllTime = "AllTime";
}
