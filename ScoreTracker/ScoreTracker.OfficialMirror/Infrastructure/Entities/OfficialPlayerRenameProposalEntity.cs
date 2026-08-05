using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     One tag that left the boards, with what the analyzer concluded and the evidence it
///     concluded it from. Written for every vanished tag, not only the actionable ones: the
///     ones that merged themselves and the ones that simply got passed are the only way to
///     tell whether the rule is still catching what it should.
///     <para>
///         The username columns are text rather than joins on purpose — they survive the merge
///         that deletes the old player row, which makes this table the audit trail for an
///         operation that cannot be undone.
///     </para>
/// </summary>
internal sealed class OfficialPlayerRenameProposalEntity
{
    [Key] public int Id { get; set; }
    public Guid MixId { get; set; }
    public int OldPlayerId { get; set; }

    /// <summary>Null when nothing was found to point at — a tag that dropped off the boards.</summary>
    public int? NewPlayerId { get; set; }

    [MaxLength(100)] public string OldUsername { get; set; } = string.Empty;
    [MaxLength(100)] public string? NewUsername { get; set; }

    /// <summary>See <c>VanishVerdicts</c>: what the analyzer decided, distinct from what was done.</summary>
    [MaxLength(20)] public string Verdict { get; set; } = string.Empty;

    public int OldPlacements { get; set; }
    public int BoardsPresent { get; set; }

    /// <summary>The number that identifies a person. Perfect games are excluded — everyone has those.</summary>
    public int ExactNonPgMatches { get; set; }

    public int ExactPerfectGames { get; set; }

    /// <summary>The next-best candidate's exact matches, so the desk can see how close the call was.</summary>
    public int RunnerUpExactMatches { get; set; }

    /// <summary>Boards where a score this tag held should still be ranking and nobody is.</summary>
    public int SuspiciousAbsences { get; set; }

    public bool AvatarMatched { get; set; }

    /// <summary>See <c>ProposalStatuses</c>: the lifecycle, including whether a human was involved.</summary>
    [MaxLength(20)] public string Status { get; set; } = "Pending";

    public int CreatedSnapshotId { get; set; }
}
