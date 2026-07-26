namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Bus trigger: compute and store every existing player's folder standings, one player at a
///     time, for every primary mix. Seeded rows are silent by construction — the backfill writes
///     state without emitting milestones, so shipping the feature never floods a Discord channel
///     (docs/design/folder-level-progression.md §7.6).
///     <para>
///         Published from the admin dashboard and never scheduled. The sweep touches every user's
///         whole score history, which is the shape that took production SQL down on 2026-07-10 —
///         it runs when somebody chooses to run it, not inside a deploy.
///     </para>
///     Takes no mix: both mixes go in one pass, since Phoenix 2 is a rounding error next to
///     Phoenix and splitting them would only mean pressing the button twice.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillFolderLevelsCommand
{
}
