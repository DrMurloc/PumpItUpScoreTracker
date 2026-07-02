using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Ucs.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;

namespace ScoreTracker.CompositionRoot;

/// <summary>
///     The complete list of vertical EF model contributions. Every extracted vertical adds
///     its contribution here — this list feeds the design-time factory and the integration
///     test fixture, so omitting one silently drops that vertical's tables from scaffolded
///     migrations.
/// </summary>
public static class VerticalModelContributions
{
    public static IDbModelContribution[] All()
    {
        return new IDbModelContribution[]
        {
            new CatalogModelContribution(), new ChartIntelligenceModelContribution(), new CommunitiesModelContribution(), new EventCompetitionModelContribution(), new OfficialMirrorModelContribution(), new PlayerProgressModelContribution(), new ScoreLedgerModelContribution(), new UcsModelContribution(), new WeeklyChallengeModelContribution()
        };
    }
}
