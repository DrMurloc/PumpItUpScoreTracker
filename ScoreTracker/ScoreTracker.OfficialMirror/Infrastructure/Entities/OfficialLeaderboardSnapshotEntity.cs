using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     One sweep run per row: the run-state record while it executes (Stage + counts +
///     Error) and the snapshot anchor once sealed. Only rows with CompletedAt set are
///     visible to reads — an unsealed snapshot is an in-flight or failed run, never data.
///     IsBaseline marks a snapshot whose highlights are deliberately skipped (the seed
///     from legacy tables, or a mix's first-ever sweep).
/// </summary>
internal sealed class OfficialLeaderboardSnapshotEntity
{
    [Key] public int Id { get; set; }
    public Guid MixId { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    ///     Stamped by every progress checkpoint. The overlap guard trusts only runs whose
    ///     heartbeat is fresh — a process killed mid-sweep stops beating and releases the
    ///     lock within minutes instead of holding it until the janitor.
    /// </summary>
    public DateTimeOffset LastProgressAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsBaseline { get; set; }
    [MaxLength(40)] public string Stage { get; set; } = string.Empty;
    public int BoardsExpected { get; set; }
    public int BoardsWritten { get; set; }
    public int BoardsSkipped { get; set; }
    [MaxLength(2000)] public string? Error { get; set; }
}
