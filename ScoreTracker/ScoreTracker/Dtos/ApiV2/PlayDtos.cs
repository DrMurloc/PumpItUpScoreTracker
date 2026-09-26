namespace ScoreTracker.Web.Dtos.ApiV2;

/// <summary>
///     The body of <c>POST api/v2/players/me/plays</c> (docs/design/rise.md §6.3): plays a tool
///     observed for the caller. <paramref name="Source" /> names the tool — one to thirty-two
///     letters, digits, dots, underscores or dashes — and is what the journal shows as the origin.
///     <paramref name="RecordBrokenAsBest" /> says whether a break on a chart the player has never
///     passed is seated as their best; omitted, the mix's default applies (the one the import page reads).
/// </summary>
public sealed record RecordPlaysRequestDto(string? Mix, string? Source, IReadOnlyList<ObservedPlayDto>? Plays,
    bool? RecordBrokenAsBest = null);

/// <summary>
///     One play as the tool saw it. The chart is named by <paramref name="ChartId" /> or by song,
///     type and level on the request's mix. The judgments — the five counts and
///     <paramref name="MaxCombo" /> — are optional and travel together: all six or none. When sent,
///     they must reproduce the score to within a point (the formula is exact to ±1), or the whole
///     request is refused. When left out, the score is taken as read.
///     <paramref name="Award" /> is optional, in the mix's own shorthand (<c>PG</c> … on a plate mix,
///     <c>PG</c> / <c>FC</c> / <c>NM</c> on Rise). With judgments it has to be the award they earn;
///     without them it is taken as sent, as long as the score agrees — only 1,000,000 is a Perfect
///     Game. A broken play carries none.
/// </summary>
public sealed record ObservedPlayDto(
    Guid? ChartId,
    string? SongName,
    string? ChartType,
    int? Level,
    int? Perfects,
    int? Greats,
    int? Goods,
    int? Bads,
    int? Misses,
    int? MaxCombo,
    int Score,
    bool IsBroken,
    string? Award,
    DateTimeOffset PlayedAt);

/// <summary>What the write did: how many plays were recorded, on which mix.</summary>
public sealed record RecordPlaysResultDto(int Recorded, string Mix, string ScoringModel);

/// <summary>
///     The body of <c>POST api/v2/players/me/sittings/close</c> (docs/design/rise.md D25): the mix whose
///     open sittings end now. Required, as the mix is on the plays write.
/// </summary>
public sealed record CloseSittingsRequestDto(string? Mix);
