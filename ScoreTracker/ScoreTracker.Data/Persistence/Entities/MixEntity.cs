using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class MixEntity
{
    [Key] public Guid Id { get; set; }

    [Required] [MaxLength(10)] public string Name { get; set; } = string.Empty;

    /// <summary>Display order for pickers: oldest mix lowest. Values mirror the timeline in docs/design/legacy-mixes.md.</summary>
    public int SortOrder { get; set; }

    /// <summary>Primary mixes (Phoenix 2 / Phoenix / XX) show directly in the mix picker; the rest live behind "More".</summary>
    public bool IsPrimary { get; set; }
}