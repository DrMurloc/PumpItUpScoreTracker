using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     A detected likely rename: a tag that vanished this snapshot whose top-50 placements
///     substantially reappeared under a new tag with the same avatar. Nothing merges until
///     an admin accepts; the username text columns survive the merge as the audit trail.
/// </summary>
internal sealed class OfficialPlayerRenameProposalEntity
{
    [Key] public int Id { get; set; }
    public Guid MixId { get; set; }
    public int OldPlayerId { get; set; }
    public int NewPlayerId { get; set; }
    [MaxLength(100)] public string OldUsername { get; set; } = string.Empty;
    [MaxLength(100)] public string NewUsername { get; set; } = string.Empty;
    public bool AvatarMatched { get; set; }
    public int Top50Overlap { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public int CreatedSnapshotId { get; set; }
}
