using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Ledger-internal port for the score calculator's population reads
///     (docs/design/phoenix-score-calculator.md D9/D8/D15). Both are single grouped
///     passes over the record table — the census stays in SQL, the judged rows come
///     back raw because grading them needs the mix's floor table, which is the
///     handler's business.
/// </summary>
internal interface IScorePopulationRepository
{
    /// <summary>
    ///     Non-broken Singles/Doubles bests banded by score per level, ascending.
    ///     A level nobody holds a best on is absent, not zero.
    /// </summary>
    Task<IReadOnlyList<LevelScorePopulation>> GetPopulationByLevel(MixEnum mix,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Every judgement-carrying, non-broken best in the mix: the score and the five
    ///     counts, plus the solved combo where one exists.
    /// </summary>
    Task<IReadOnlyList<JudgedBest>> GetJudgedBests(MixEnum mix, CancellationToken cancellationToken);
}

/// <summary>One judged best, raw — the spread handler's aggregation input.</summary>
internal sealed record JudgedBest(int Score, int Perfects, int Greats, int Goods, int Bads, int Misses,
    int? MaxCombo);
