using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

// One import attempt. Kind and Outcome are stored as their enum NAMES rather than ints: the
// point of this table is being readable straight from SQL when a player reports a bad import,
// and a column of 0s and 2s is not that.
[Index(nameof(UserId), nameof(StartedAt))]
internal sealed class ImportResultEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public Guid MixId { get; set; }

    /// <summary>Standard | Check | DeepScan.</summary>
    [Required]
    [MaxLength(16)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The game card the run was pointed at, when the caller knew one.</summary>
    [MaxLength(100)]
    public string? CardId { get; set; }

    [Required] public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    ///     Null means nothing ever closed this run. Together with a null Outcome that is the
    ///     "never reported back" state — an interrupted process, not a failure anybody saw.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Completed | PiuGameError | PiuScoresError. Never exception text.</summary>
    [MaxLength(16)]
    public string? Outcome { get; set; }

    /// <summary>
    ///     The score session this run saved into, once one was opened. Nullable and unconstrained
    ///     by design: a run that fails before its first save has no session, and undoing a session
    ///     must not have to reach back into this table.
    /// </summary>
    public Guid? SessionId { get; set; }
}
