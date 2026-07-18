using System.Diagnostics.CodeAnalysis;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     The per-judgement breakdown of one play as the game screen reports it. Note count is
///     the sum — the game has no "note" the player didn't judge.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record JudgementCounts(int Perfects, int Greats, int Goods, int Bads, int Misses)
{
    public int NoteCount => Perfects + Greats + Goods + Bads + Misses;
}
