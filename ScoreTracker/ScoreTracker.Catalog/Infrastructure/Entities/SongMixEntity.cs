using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities;

/// <summary>
///     A song's channel on one mix (docs/design/song-channels.md §3): the first fact stored per
///     song per mix. Keyed on (SongId, MixId) with no surrogate id, the way the contribution's
///     other composite tables are; a row exists to say the channel, so <see cref="Channel" /> is
///     required and a missing row means unknown. Membership stays ChartMix's. Catalog-owned: the
///     table and its two foreign keys onto Data's Song and Mix are declared by
///     <see cref="Wiring.CatalogModelContribution" />.
/// </summary>
internal sealed class SongMixEntity
{
    [Required] public Guid SongId { get; set; }

    [Required] public Guid MixId { get; set; }

    /// <summary>The <see cref="SharedKernel.Enums.Channel" /> enum name: <c>KPop</c>, <c>WorldMusic</c>.</summary>
    [Required] [MaxLength(16)] public string Channel { get; set; } = string.Empty;
}
