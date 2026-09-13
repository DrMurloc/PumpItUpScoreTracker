using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities;

/// <summary>
///     One game patch of a mix (docs/design/chart-versions.md §2). Catalog-owned: the table is
///     registered by <see cref="Wiring.CatalogModelContribution" />, which also declares the foreign
///     key from the shared ChartMix row's <c>AddedInVersionId</c> onto it.
/// </summary>
internal sealed class MixVersionEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid MixId { get; set; }

    /// <summary>The number the game prints on its update notice, stored bare: <c>1.01.0</c>. Unique per mix.</summary>
    [Required] [MaxLength(16)] public string Name { get; set; } = string.Empty;

    /// <summary>The Korean notice date; null on a legacy patch nobody dated (D5).</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>The ordering truth — by/after-version filters compare on it, never on the name.</summary>
    public int SortOrder { get; set; }
}
