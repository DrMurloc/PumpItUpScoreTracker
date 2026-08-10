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

    /// <summary>
    ///     Completed | PiuGameError | CredentialRejected | PiuScoresError | Interrupted. Never
    ///     exception text.
    ///     <para>
    ///         ⚠ Sized for the enum's NAMES, which is why 16 was not enough:
    ///         <c>CredentialRejected</c> is 18 characters, so closing a rejected-credential run
    ///         threw a truncation error inside the consumer's <c>finally</c> — leaving the run
    ///         open and reading, on the player's screen, as "never reported back" rather than
    ///         "check your password". A new member longer than this needs the column widened
    ///         with it.
    ///     </para>
    /// </summary>
    [MaxLength(32)]
    public string? Outcome { get; set; }

    /// <summary>
    ///     The score session this run saved into, once one was opened. Nullable and unconstrained
    ///     by design: a run that fails before its first save has no session, and undoing a session
    ///     must not have to reach back into this table.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    ///     How many records this run actually changed, stamped when the run closes.
    ///     <para>
    ///         Recorded here rather than read off <c>ScoreSession.ScoreCount</c>, which cannot
    ///         answer for a run that just finished: that counter is written when the score batch
    ///         DRAINS, on a ~2 minute in-memory debounce, so an early look or an app restart
    ///         inside the window leaves it at zero forever while the journal holds the rows
    ///         (observed 2026-08-08: a run with 7 journal rows and a ScoreCount of 0). The import
    ///         already knows what it saved, so it says so itself.
    ///     </para>
    ///     Null on a run that never reported back, which is the one case nobody can count.
    /// </summary>
    public int? ScoreCount { get; set; }

    /// <summary>
    ///     When the player was told this run was interrupted. Null on every run that never needed
    ///     saying — which is almost all of them — and on the one interrupted run whose notice is
    ///     still waiting to be shown.
    ///     <para>
    ///         Kept on the run rather than in a UiSetting so it is per-run by construction: a
    ///         second interruption is a second unacknowledged row and raises the notice again,
    ///         with no key-naming scheme to get wrong
    ///         (docs/design/import-restart-recovery.md §7).
    ///     </para>
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }
}
