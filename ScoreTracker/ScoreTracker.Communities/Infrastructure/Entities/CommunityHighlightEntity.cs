using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities;

/// <summary>
///     A community-scoped big win, one row per (event × community the winner belongs to).
///     The <see cref="Payload" /> is a JSON list of Contracts.SignificantWin, written whole and
///     read whole. (CommunityId, OccurredAt) serves the feed read; the OccurredAt index serves the
///     weekly purge. EventId dedupes a win that fanned out to several shared communities.
/// </summary>
[Index(nameof(CommunityId), nameof(OccurredAt))]
[Index(nameof(OccurredAt))]
internal sealed class CommunityHighlightEntity
{
    [Key] public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid CommunityId { get; set; }
    public Guid UserId { get; set; }
    public Guid MixId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? SessionId { get; set; }
    [Required] public string Payload { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
}
