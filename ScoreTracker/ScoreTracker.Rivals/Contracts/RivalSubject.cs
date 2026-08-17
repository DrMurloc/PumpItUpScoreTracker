namespace ScoreTracker.Rivals.Contracts;

/// <summary>
///     A rival, resolved. The ONE abstraction every rival surface consumes, so the tag/user
///     duality lives in exactly one place (docs/design/rivals.md §2.1).
///     <para>
///         <see cref="Capabilities" /> is what makes a shared player-summary component honest: it
///         is handed a subject rather than a user id, and renders the sections that subject can
///         actually answer for. A board-only rival is short by construction, not by accident.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RivalSubject(
    Guid EdgeId,
    Guid? UserId,
    string? Tag,
    string DisplayName,
    Uri? Avatar,
    bool IsOnCurrentBoards,
    RivalCapabilities Capabilities,
    DateTimeOffset AddedAt)
{
    /// <summary>No account behind the tag — the mirror is everything we know about them.</summary>
    public bool IsGhost => UserId == null;

    public bool Can(RivalCapabilities capability) => Capabilities.HasFlag(capability);
}

/// <summary>
///     Who a head-to-head is against. Lighter than <see cref="RivalSubject" /> because a comparison
///     needs no edge: any site player the visibility port lets you look at can be compared, rival or
///     not, so the record carries who they are and nothing about how they came to be on your roster.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HeadToHeadSubject(Guid UserId, string DisplayName, Uri? Avatar);

/// <summary>
///     What a subject can answer for. Flags rather than a type hierarchy because the combinations
///     are real: a site player with no linked board tag has scores but no standings, and a linked
///     one has both.
/// </summary>
[Flags]
public enum RivalCapabilities
{
    None = 0,

    /// <summary>Best attempts on any chart, live.</summary>
    LiveScores = 1,

    /// <summary>Whole-folder comparison — needs every chart in the folder, not a scattering.</summary>
    FolderCompare = 2,

    /// <summary>Titles, ratings, folder levels.</summary>
    Progression = 4,

    /// <summary>PUMBILITY rank and board placements from the weekly mirror.</summary>
    OfficialStandings = 8
}
