using ScoreTracker.WeeklyChallenge.Wiring;
using ScoreTracker.Catalog.Wiring;
using ScoreTracker.ChartIntelligence.Wiring;
using ScoreTracker.Communities.Wiring;
using ScoreTracker.EventCompetition.Wiring;
using ScoreTracker.Identity.Wiring;
using ScoreTracker.PlayerProgress.Wiring;
using ScoreTracker.OfficialMirror.Wiring;
using System;
using System.Linq;
using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Wiring;
using ScoreTracker.Ucs.Contracts;
using ScoreTracker.Ucs.Contracts.Commands;
using ScoreTracker.Ucs.Domain;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Vertical-assembly ratchets (ADR-001 D2): within a vertical, only Contracts/ (the
///     published surface) and Wiring/ (the AddXxx DI extension + EF model contribution)
///     are public; everything else is internal and reachable only through those.
///     Rules are added per extracted vertical and never removed.
/// </summary>
public sealed class VerticalBoundaryTests
{
    // One entry per extracted vertical: the public Wiring marker type anchors the
    // assembly and its root namespace. New verticals add themselves here.
    public static TheoryData<Type> VerticalWiringMarkers => new()
    {
        typeof(Ucs.Wiring.UcsRegistrationExtensions),
        typeof(ScoreLedger.Wiring.ScoreLedgerRegistrationExtensions),
        typeof(OfficialMirror.Wiring.OfficialMirrorRegistrationExtensions),
        typeof(Catalog.Wiring.CatalogRegistrationExtensions),
        typeof(ChartIntelligence.Wiring.ChartIntelligenceRegistrationExtensions),
        typeof(WeeklyChallenge.Wiring.WeeklyChallengeRegistrationExtensions),
        typeof(EventCompetition.Wiring.EventCompetitionRegistrationExtensions),
        typeof(Communities.Wiring.CommunitiesRegistrationExtensions),
        typeof(PlayerProgress.Wiring.PlayerProgressRegistrationExtensions),
        typeof(Identity.Wiring.IdentityRegistrationExtensions),
        typeof(HomePage.Wiring.HomePageRegistrationExtensions)
    };

    [Theory]
    [MemberData(nameof(VerticalWiringMarkers))]
    public void VerticalPublicSurfaceIsContractsAndWiringOnly(Type wiringMarker)
    {
        var root = wiringMarker.Namespace!.Substring(0,
            wiringMarker.Namespace!.Length - ".Wiring".Length);
        var offenders = wiringMarker.Assembly.GetTypes()
            .Where(t => t.IsPublic)
            .Where(t => t.Namespace == null
                        || (!t.Namespace.StartsWith(root + ".Contracts", StringComparison.Ordinal)
                            && t.Namespace != root + ".Wiring"))
            .Select(t => t.FullName)
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Only Contracts/ and Wiring/ may be public in {root}: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void MassTransitDiscoversTheOfficialMirrorsInternalConsumers()
    {
        // OfficialLeaderboardSaga consumes StartLeaderboardImportCommand. Same rationale
        // as the ScoreLedger tripwire below: assembly scanning skips internal consumers,
        // so the AddOfficialMirrorConsumers hook is the registration path.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddOfficialMirrorConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.OfficialMirror.Application.OfficialLeaderboardSaga));
    }

    [Fact]
    public void MassTransitDiscoversTheCatalogsInternalConsumers()
    {
        // PiuCenterCrawlSaga consumes CrawlPiuCenterCommand — the Catalog's first bus
        // consumer. Assembly scanning skips internal consumers, so the
        // AddCatalogConsumers hook is the registration path; if it stops covering the
        // saga, the weekly crawl silently never runs and skill data goes stale.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddCatalogConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(Catalog.Application.PiuCenterCrawlSaga));
    }

    [Fact]
    public void MassTransitDiscoversTheChartIntelligencesInternalConsumers()
    {
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddChartIntelligenceConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.ChartIntelligence.Application.TierListSaga));
        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.ChartIntelligence.Application.ScoringDifficultySaga));
    }

    [Fact]
    public void MassTransitDiscoversTheWeeklyChallengesInternalConsumers()
    {
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddWeeklyChallengeConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.WeeklyChallenge.Application.WeeklyTournamentSaga));
    }

    [Fact]
    public void MassTransitDiscoversThePlayerProgressInternalConsumers()
    {
        // PlayerRatingSaga is the rating pipeline (stats + Pumbility after every score
        // import); TitleSaga and PlayerHistorySaga ride the same event streams. All are
        // internal, so the AddPlayerProgressConsumers hook is the registration path — if
        // it stops covering them, ratings silently freeze while imports keep succeeding.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddPlayerProgressConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.PlayerProgress.Application.PlayerRatingSaga));
        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.PlayerProgress.Application.TitleSaga));
        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.PlayerProgress.Application.PlayerHistorySaga));
    }

    [Fact]
    public void MassTransitDiscoversTheCommunitiesInternalConsumers()
    {
        // CommunitySaga fans six event streams out to community Discord channels — and it
        // used to be the Application assembly's AddConsumers marker type in Program.cs.
        // Internal now, so the AddCommunitiesConsumers hook is the registration path; if
        // it stops covering the saga, every community feed silently goes quiet.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddCommunitiesConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.Communities.Application.CommunitySaga));
    }

    [Fact]
    public void MassTransitDiscoversTheEventCompetitionsInternalConsumers()
    {
        // QualifiersSaga consumes ScoreImportCompletedEvent (auto-qualifier registration);
        // MarchOfMurlocsHandler consumes the MoM trigger messages. Both are internal, so
        // the AddEventCompetitionConsumers hook is the registration path — without it,
        // qualifier auto-submission and MoM season rotation silently stop.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddEventCompetitionConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.EventCompetition.Application.QualifiersSaga));
        Assert.Contains(services,
            d => d.ServiceType == typeof(ScoreTracker.EventCompetition.Application.MarchOfMurlocsHandler));
    }

    [Fact]
    public void MassTransitDiscoversTheScoreLedgersInternalConsumers()
    {
        // UpdatePhoenixRecordHandler is an internal IConsumer<> (TryFireScoreCommand,
        // FlushOverdueScoreBatchesCommand). MassTransit's AddConsumers assembly scan skips
        // internal types (verified when this vertical extracted), so the vertical exposes
        // AddScoreLedgerConsumers as the explicit registration hook. If that hook ever
        // stops covering the internal consumers, score-post batching silently stops
        // draining — this wiring test is the tripwire.
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            x.AddScoreLedgerConsumers();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        Assert.Contains(services, d => d.ServiceType == typeof(UpdatePhoenixRecordHandler));
    }

    [Fact]
    public void MediatRDiscoversTheUcsVerticalsInternalHandlers()
    {
        // The vertical's handlers are internal classes behind public contract records. If the
        // host's MediatR assembly scan ever skips non-public types, every UCS page breaks at
        // runtime while unit tests (which construct the saga directly) stay green — this
        // wiring test is the tripwire.
        var services = new ServiceCollection();
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(typeof(UcsChart).Assembly));
        services.AddTransient(_ => new Mock<IBus>().Object);
        services.AddTransient(_ => new Mock<IUcsRepository>().Object);
        services.AddTransient(_ => new Mock<ICurrentUserAccessor>().Object);
        services.AddTransient(_ => new Mock<IDateTimeOffsetAccessor>().Object);
        using var provider = services.BuildServiceProvider();

        var handler = provider.GetService<IRequestHandler<RegisterUcsEntryCommand>>();

        Assert.NotNull(handler);
    }
}
