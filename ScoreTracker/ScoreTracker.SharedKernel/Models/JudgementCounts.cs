using System.Diagnostics.CodeAnalysis;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     The per-judgement breakdown of one play as the game screen reports it. Note count is
///     the sum — the game has no "note" the player didn't judge.
///     <para>
///         <see cref="MaxCombo" /> rides beside the five because it is stored and served beside
///         them, but it is not one of them: the site prints no combo, so it is solved from the
///         score and the counts (the Domain's PhoenixComboSolver) and is null wherever that
///         cannot be done — no note count, a breakdown short of the chart, a stage break.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record JudgementCounts(int Perfects, int Greats, int Goods, int Bads, int Misses,
    int? MaxCombo = null)
{
    public int NoteCount => Perfects + Greats + Goods + Bads + Misses;
}
