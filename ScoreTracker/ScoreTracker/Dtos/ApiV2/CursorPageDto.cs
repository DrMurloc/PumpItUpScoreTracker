namespace ScoreTracker.Web.Dtos.ApiV2;

/// <summary>
///     The api/v2 collection envelope. One shape for every collection, so a client writes its
///     paging loop once (docs/design/api-v2-community-tools.md §3).
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed class CursorPageDto<T>
{
    /// <summary>The rows on this page.</summary>
    public T[] Data { get; set; } = Array.Empty<T>();

    /// <summary>How many rows were asked for — not how many arrived, which is <c>Data.Length</c>.</summary>
    public int Limit { get; set; }

    /// <summary>
    ///     Total matching rows, or null where counting would cost a second full pass. Present on
    ///     bounded catalog collections, null on player data. The field stays either way so the
    ///     envelope never changes shape between endpoints.
    /// </summary>
    public int? Total { get; set; }

    /// <summary>
    ///     Absolute URL of the next page, or null on the last one. Follow it rather than
    ///     constructing it — the cursor inside is opaque and tied to this request's filters.
    /// </summary>
    public string? Next { get; set; }
}
