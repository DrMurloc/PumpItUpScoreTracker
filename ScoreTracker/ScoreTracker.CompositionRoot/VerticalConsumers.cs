using MassTransit;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartComments.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.CommunityTools.Wiring;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.HomePage.Wiring;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.Randomizer.Wiring;
using ScoreTracker.Rivals.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Seasons.Wiring;
using ScoreTracker.Translations.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;

namespace ScoreTracker.CompositionRoot;

/// <summary>
///     Registers every vertical's bus consumers. MassTransit's assembly scan skips internal
///     types, so each vertical publishes an <c>AddXxxConsumers</c> hook in its <c>Wiring/</c>, and
///     this is the one list of those hooks. A vertical with consumers adds its hook here.
///     <para>
///         Same hazard <see cref="VerticalModelContributions" /> and <see cref="VerticalAssemblies" />
///         guard: a per-vertical line whose omission is silent. A hook left off this list leaves its
///         consumers unregistered and their messages dropped, with no exception anywhere.
///     </para>
/// </summary>
public static class VerticalConsumers
{
    public static void AddAll(IRegistrationConfigurator configurator)
    {
        configurator.AddPlayerProgressConsumers();
        configurator.AddScoreLedgerConsumers();
        configurator.AddOfficialMirrorConsumers();
        configurator.AddChartIntelligenceConsumers();
        configurator.AddWeeklyChallengeConsumers();
        configurator.AddCommunityToolsConsumers();
        configurator.AddChartCommentsConsumers();
        configurator.AddTranslationsConsumers();
        configurator.AddEventCompetitionConsumers();
        configurator.AddCommunitiesConsumers();
        configurator.AddCatalogConsumers();
        configurator.AddIdentityConsumers();
        configurator.AddRandomizerConsumers();
        configurator.AddHomePageConsumers();
        configurator.AddRivalsConsumers();
        configurator.AddSeasonsConsumers();
    }
}
