using System;
using System.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The March of Murlocs on-ramp on a session (docs/design/march-of-murlocs.md D32). Three
///     states and they are ordered: a night already on a board wears the chip, a night holding a
///     session-shaped window gets the callout, and every other night gets the quiet link — which is
///     always there, because there is always a season.
/// </summary>
public sealed class SessionHeroMoMOnRampTests : ComponentTestBase
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly Guid Board = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 8, 5, 20, 0, TimeSpan.Zero);

    public SessionHeroMoMOnRampTests()
    {
        Services.AddSingleton(new Mock<IRandomNumberGenerator>().Object);
        this.RenderInteractive();
    }

    private IRenderedComponent<SessionHero> Render(MoMOnRamp? onRamp) =>
        RenderComponent<SessionHero>(p => p
            .Add(h => h.Breakdown, Breakdown())
            .Add(h => h.MoM, onRamp));

    [Fact]
    public void ANightWithNothingSpecialAboutItStillOffersTheQuietLink()
    {
        var cut = Render(new MoMOnRamp(Board, null, null));

        var link = cut.Find("[data-testid=mom-onramp-link]");
        Assert.Contains("Record as a March of Murlocs session", link.TextContent);
        Assert.Equal($"/MarchOfMurlocs/Record/{Board}", link.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid=mom-onramp-callout]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-onramp-recorded]"));
    }

    [Fact]
    public void ASessionShapedNightGetsTheCalloutWithTheNumbersThatDecidedIt()
    {
        // The 8 August night: 31 Doubles charts, 61.5 minutes of song, 43.5 of rest in 1:45.
        var candidate = new MoMSessionCandidate(Board, ChartType.Double, 31,
            TimeSpan.FromMinutes(61.5), TimeSpan.FromMinutes(43.5), Start, Start.AddMinutes(105), 0);

        var cut = Render(new MoMOnRamp(Board, null, candidate));

        var callout = cut.Find("[data-testid=mom-onramp-callout]");
        Assert.Contains("This looks like a March of Murlocs session.", callout.TextContent);
        Assert.Contains("Doubles", callout.TextContent);
        Assert.Contains("31", callout.TextContent);
        Assert.Contains("1:01:30", callout.TextContent);
        Assert.Contains("43:30", callout.TextContent);
        Assert.DoesNotContain("stage break", callout.TextContent);
        Assert.Equal($"/MarchOfMurlocs/Record/{Board}",
            cut.Find("[data-testid=mom-onramp-record]").GetAttribute("href"));
        // The quiet link stays under the title as well: it is always there, because there is
        // always a season, and the callout is the loud version of the same offer.
        Assert.NotEmpty(cut.FindAll("[data-testid=mom-onramp-link]"));
    }

    [Fact]
    public void StageBreaksInsideTheWindowAreCountedInTheCallout()
    {
        var candidate = new MoMSessionCandidate(Board, ChartType.Single, 24,
            TimeSpan.FromMinutes(58), TimeSpan.FromMinutes(47), Start, Start.AddMinutes(100), 2);

        var cut = Render(new MoMOnRamp(Board, null, candidate));

        var callout = cut.Find("[data-testid=mom-onramp-callout]");
        Assert.Contains("Singles", callout.TextContent);
        Assert.Contains("stage breaks", callout.TextContent);
    }

    [Fact]
    public void ANightAlreadyOnABoardWearsTheChipAndNothingElse()
    {
        var recorded = new MoMRecordedNight(Guid.NewGuid(), ChartType.Double, 1, 42, 59319);

        var cut = Render(new MoMOnRamp(Board, recorded, null));

        var chip = cut.Find("[data-testid=mom-onramp-recorded]");
        Assert.Contains("March of Murlocs session", chip.TextContent);
        Assert.Contains("1st on Doubles", chip.TextContent);
        Assert.Contains("59,319 points", chip.TextContent);
        Assert.Equal($"/MarchOfMurlocs/Session/{recorded.SessionId}", chip.GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid=mom-onramp-link]"));
    }

    [Fact]
    public void ASessionAskedNothingOfMarchOfMurlocsSaysNothingAboutIt()
    {
        var cut = Render(null);

        Assert.Empty(cut.FindAll("[data-testid=mom-onramp-link]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-onramp-callout]"));
        Assert.Empty(cut.FindAll("[data-testid=mom-onramp-recorded]"));
    }

    private static SessionBreakdown Breakdown()
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(Name.From("Slam"), SongType.Arcade, new Uri("https://example.invalid/s.png"),
                TimeSpan.FromSeconds(128), Name.From("artist"), null),
            ChartType.Double, DifficultyLevel.From(24), MixEnum.Phoenix, null, null);
        var row = new RecentSessionsPage.ScoreEventRecord(chart.Id, Start, 980000, "Marvelous Game", false,
            "officialImport", Session, ScoreEventClassification.NewPass, null);
        var scores = new[] { new SessionScore(row, chart, HighlightFlags.None, null) };

        return new SessionBreakdown(
            new RecentSessionsPage.SessionGroup(Session, null, MixEnum.Phoenix, "officialImport",
                Start, Start.AddMinutes(118), new[] { row }),
            new ScoreSessionRecord(Session, Guid.NewGuid(), MixEnum.Phoenix, "officialImport",
                "DRMURLOC #7251", "01", Start, Start.AddMinutes(118), 1, 1, 0),
            new System.Collections.Generic.Dictionary<Guid, Chart> { [chart.Id] = chart }, scores,
            new SessionCeremony(64612, 64466, 64612, 22.62, 22.68, null, null, 22.68, 23.4, 131, 148,
                Start.AddDays(-6)),
            Array.Empty<PlayerMilestoneRecord>(),
            Array.Empty<SessionTitleBarModel>());
    }
}
