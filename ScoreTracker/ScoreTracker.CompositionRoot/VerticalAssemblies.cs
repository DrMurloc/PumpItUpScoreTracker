using System.Reflection;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.CommunityTools.Wiring;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.HomePage.Wiring;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.Randomizer.Wiring;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.WeeklyChallenge.Wiring;

namespace ScoreTracker.CompositionRoot;

/// <summary>
///     Every vertical assembly, for the host's MediatR scan.
///     <para>
///         This list exists because it was once a literal in <c>Program.cs</c> and CommunityTools
///         was left off it. Nothing failed to compile and no test failed: the vertical's 33 handlers
///         simply were not registered, and the first anyone knew was a page throwing "No service for
///         type IRequestHandler&lt;GetShareWithAllToolsQuery, Boolean&gt;" in a field test.
///     </para>
///     <para>
///         Same hazard <see cref="VerticalModelContributions" /> guards for EF: a per-vertical line
///         that must be added by hand, whose omission is silent. Here the list is one place and
///         <c>MediatRHandlerRegistrationTests</c> checks it against the assemblies that actually
///         contain handlers, so a new vertical cannot be forgotten.
///     </para>
/// </summary>
public static class VerticalAssemblies
{
    public static Assembly[] All()
    {
        return new[]
        {
            typeof(CatalogRegistrationExtensions).Assembly,
            typeof(ChartIntelligenceRegistrationExtensions).Assembly,
            typeof(CommunitiesRegistrationExtensions).Assembly,
            typeof(CommunityToolsRegistrationExtensions).Assembly,
            typeof(EventCompetitionRegistrationExtensions).Assembly,
            typeof(HomePageRegistrationExtensions).Assembly,
            typeof(IdentityRegistrationExtensions).Assembly,
            typeof(OfficialMirrorRegistrationExtensions).Assembly,
            typeof(PlayerProgressRegistrationExtensions).Assembly,
            typeof(RandomizerRegistrationExtensions).Assembly,
            typeof(ScoreLedgerRegistrationExtensions).Assembly,
            typeof(WeeklyChallengeRegistrationExtensions).Assembly
        };
    }
}
