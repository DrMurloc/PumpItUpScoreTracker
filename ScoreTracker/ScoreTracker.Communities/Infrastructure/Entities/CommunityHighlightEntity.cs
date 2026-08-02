using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities;

/// <summary>
///     An audience-index row: this event is visible to this community, one row per (event ×
///     community the winner belongs to). (CommunityId, OccurredAt) serves the feed read; the
///     OccurredAt index serves the weekly purge. EventId dedupes a win that fanned out to several
///     shared communities, and is what the payload is fetched by.
///     <para>
///         <see cref="Payload" /> and <see cref="SchemaVersion" /> are RETAINED BUT NO LONGER
///         WRITTEN (docs/design/rivals.md D33). The wins live once now, keyed by player, in
///         scores.PlayerHighlight — a community is an audience, not an owner. Rows written before
///         the move keep their payload because it is the backfill's only source; new rows leave
///         both empty. Neither column is read.
///     </para>
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
