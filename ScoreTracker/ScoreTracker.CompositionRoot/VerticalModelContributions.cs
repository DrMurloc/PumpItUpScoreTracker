using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Ucs.Wiring;

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
            new OfficialMirrorModelContribution(), new ScoreLedgerModelContribution(), new UcsModelContribution()
        };
    }
}
