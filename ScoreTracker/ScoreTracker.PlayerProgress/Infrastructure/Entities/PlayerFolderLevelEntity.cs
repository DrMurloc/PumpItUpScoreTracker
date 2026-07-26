using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities;

/// <summary>
///     One player's standing in one folder. Keyed on (user, mix, type, level) — no surrogate id,
///     because the folder itself is the identity and every write is an upsert.
///     <see cref="Level" /> holds the player count for co-op folders.
/// </summary>
internal sealed class PlayerFolderLevelEntity
{
    public Guid UserId { get; set; }
    public Guid MixId { get; set; }

    [MaxLength(32)] public string ChartType { get; set; } = string.Empty;

    public int Level { get; set; }

    /// <summary>Charts in the folder, including ones the player has never played.</summary>
    public int Size { get; set; }

    public int Played { get; set; }

    /// <summary>Mean across played charts only; 0 when <see cref="Played" /> is 0.</summary>
    public int AverageScore { get; set; }

    /// <summary>
    ///     The score at the completion tier's position, best-first — what the folder grade reads
    ///     off. Stored rather than derived because a milestone diff needs the previous grade and
    ///     the row alone cannot rebuild a sorted score list. 0 below the first tier.
    /// </summary>
    public int TierScore { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
