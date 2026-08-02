using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Renders the hero with every section populated. Razor validates unknown COMPONENTS at
///     compile time but not unknown PARAMETERS, so a mistyped one builds clean and throws on
///     first render — which is how a made-up SongImage "Size" reached a running page. Rendering
///     the whole tree once is what catches that class.
/// </summary>
public sealed class SessionHeroTests : ComponentTestBase
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

    // DifficultyBubble gates its tooltip on RendererInfo. Last after the base's registrations —
    // reading the renderer locks the service collection.
    public SessionHeroTests() => this.RenderInteractive();

    [Fact]
    public void TheHeroRendersEverySectionItHasDataFor()
    {
        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, FullBreakdown()));

        Assert.NotEmpty(hero.FindAll("[data-testid='session-ceremony']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-title-bars']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-milestones']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-notable']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='community-peers']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-all-plays']"));
    }

    [Fact]
    public void AScoreWithNoCohortRendersWithoutAPercentile()
    {
        // Co-op and far-below-competitive charts have nothing measuring them. The row prints no
        // standing and says nothing about why — a disclaimer there confuses more than it helps.
        var breakdown = FullBreakdown();
        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        Assert.Contains("at your level", hero.Markup);
        Assert.DoesNotContain("cohort has no", hero.Markup);
    }

    [Fact]
    public void AQuietSessionStillRendersTheCeremonyBand()
    {
        // The band is the anchor: a session that moved nothing keeps the shape rather than
        // collapsing into an empty hero.
        var quiet = FullBreakdown() with
        {
            Ceremony = new SessionCeremony(64612, null, null, null, null, null, null, 22.6, 23.4, null, null, null),
            TitleBars = Array.Empty<SessionTitleBarModel>(),
            Milestones = Array.Empty<PlayerMilestoneRecord>(),
            PeerBoards = Array.Empty<SessionPeerBoard>()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, quiet));

        Assert.NotEmpty(hero.FindAll("[data-testid='session-ceremony']"));
        Assert.Empty(hero.FindAll("[data-testid='session-title-bars']"));
        // The empty state names the action that fills it rather than rendering nothing.
        Assert.NotEmpty(hero.FindAll("[data-testid='community-peers-empty']"));
    }

    private static SessionBreakdown FullBreakdown()
    {
        var single = ChartAt(ChartType.Single, 21);
        var coOp = ChartAt(ChartType.CoOp, 18);
        var rows = new[]
        {
            Row(single.Id, Start, 912400, ScoreEventClassification.NewPass),
            Row(coOp.Id, Start.AddMinutes(9), 876300, ScoreEventClassification.NewPass)
        };
        var charts = new Dictionary<Guid, Chart> { [single.Id] = single, [coOp.Id] = coOp };

        var scores = new[]
        {
            new SessionScore(rows[0], single, HighlightFlags.FolderDebut | HighlightFlags.OfficialBoardPlacement,
                new HighlightDetail(FolderDebutOrdinal: 1, PeerCount: 74, PeerBetterCount: 12,
                    PeerPercentile: 0.84, AttemptsBeforeClear: 6, OfficialPlace: 42, OfficialBoardDepth: 100,
                    OfficialAsOf: Start.AddDays(-6))),
            // No detail at all: the co-op row is the one with nothing measuring it.
            new SessionScore(rows[1], coOp, HighlightFlags.None, null)
        };

        return new SessionBreakdown(
            new RecentSessionsPage.SessionGroup(Session, null, MixEnum.Phoenix, "officialImport",
                Start, Start.AddMinutes(9), rows),
            new ScoreSessionRecord(Session, Guid.NewGuid(), MixEnum.Phoenix, "officialImport",
                "DRMURLOC #7251", "01", Start, Start.AddMinutes(9), 2, 2, 0),
            charts, scores,
            new SessionCeremony(64612, 64466, 64612, 22.62, 22.68, null, null, 22.68, 23.4, 131, 148,
                Start.AddDays(-6)),
            new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, Session, Start, null, null,
                    "Advanced Lv. 3", null)
            },
            new[] { new SessionTitleBarModel("21", "Advanced Lv. 4", 0.61, 0.78, 3120, 4000) },
            new[]
            {
                new SessionPeerBoard(single, new[]
                {
                    new CommunityPeerScore(Guid.NewGuid(), Name.From("MIDNIGHT"),
                        new[] { Name.From("Arrow Eclipse") }, 22.4, PhoenixScore.From(930000),
                        PhoenixPlate.TalentedGame, false)
                })
            },
            new Dictionary<Guid, User>());
    }

    private static Chart ChartAt(ChartType type, int level)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }

    private static RecentSessionsPage.ScoreEventRecord Row(Guid chartId, DateTimeOffset at, int score,
        ScoreEventClassification classification)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, at, score, "Fair Game", false, "seed",
            Session, classification, null);
    }
}
