using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

public sealed record Chart(Guid Id, MixEnum OriginalMix, Song Song, ChartType Type, DifficultyLevel Level, MixEnum Mix,
    Name? StepArtist,
    int? NoteCount,
    IReadOnlySet<Skill> Skills,
    LegacySlot? Slot = null,
    int? PlayerCountOverride = null)
{
    public string DifficultyString => $"{Type.GetShortHand()}{Level}";

    /// <summary>
    ///     Human-facing difficulty. Slot-aware because pre-Exceed slots are identity:
    ///     the same song can carry Hard 6 AND Crazy 6 — "S6" alone is ambiguous there
    ///     (docs/design/legacy-mixes.md).
    /// </summary>
    public string DifficultyDisplay => Slot != null ? $"{Slot.Value.GetName()} {Level}" : DifficultyString;

    /// <summary>
    ///     Mainline co-op charts have no difficulty, so their Level slot historically
    ///     stores the player count — but legacy Routine-era co-ops carry BOTH a real
    ///     difficulty (in Level) and a player count, so persistence supplies the
    ///     override (docs/design/legacy-mixes.md). Null override = the old derivation,
    ///     byte-identical for every pre-existing chart.
    /// </summary>
    public int PlayerCount => PlayerCountOverride ?? (Type == ChartType.CoOp ? Level : 1);
}