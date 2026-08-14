using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     A community mute. Lifted rows are retained — the history answers "why can't I post" months
///     later — so reads filter on <see cref="LiftedAt" />.
/// </summary>
// UserId is whose mic this takes and owns the row for purge. RestrictedByUserId is the moderator,
// a different person — [PurgeKey] says so. Like DeletedByUserId on CommentEntity, the moderator
// pointer may outlive its account.
// The (UserId, CommunityId) index is declared in ChartCommentsModelContribution rather than here:
// it is UNIQUE over active rows only (filtered on LiftedAt IS NULL), which an attribute cannot
// express — the saga's check-then-insert is the polite path, and the index is what makes two
// racing moderators land on one mute instead of two.
[Index(nameof(CommunityId))]
[PurgeKey(nameof(UserId))]
internal sealed class CommentRestrictionEntity
{
    [Key] public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid CommunityId { get; set; }

    public Guid RestrictedByUserId { get; set; }

    [MaxLength(500)] public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LiftedAt { get; set; }
}
