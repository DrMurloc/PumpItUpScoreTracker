namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One level's personal-best population in one mix: how many non-broken Singles/Doubles
///     bests exist there, banded by score. The cuts are the grade floors the two mixes share
///     (950/970/980/990/995k) plus the 900,000 line both mixes hang a floor on, so one shape
///     serves either mix's page and the presentation maps bands to that mix's letters
///     (docs/design/phoenix-score-calculator.md D9). Each band counts scores in
///     [cut, next cut); Below900k is everything under the line.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LevelScorePopulation(
    int Level,
    int Total,
    int Below900k,
    int From900k,
    int From950k,
    int From970k,
    int From980k,
    int From990k,
    int From995k);
