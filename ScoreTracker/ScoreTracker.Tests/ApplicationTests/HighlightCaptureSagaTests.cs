using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class HighlightCaptureSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task CrownFlagsChartsInTheCurrentTop50()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenTop50(chart.Id);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.PumbilityTop50) && x.Level == 20
                && x.Detail != null && x.Detail.PumbilityRank == 1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheRetiredTitleProgressFlagIsNeverWritten()
    {
        // Title progress is no longer a per-row flag. Moonlight S18 IS [DRILL] Lv.4 and would
        // have claimed it under the old rule, so it stays the case that proves the flag is gone.
        // The enum member survives only so historical rows keep their meaning.
        var skillChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18)
            .WithSongName("Moonlight").Build();
        var plainChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(skillChart, plainChart);
        ctx.GivenBest(skillChart, 972000);
        ctx.GivenBest(plainChart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassesEvent(skillChart, plainChart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.TitleProgress))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScoreQualityFlagsTopDecileAgainstComparablePlayers()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        // Ten comparable scores, all below — tie-inclusive percentile 1.0.
        ctx.GivenCohort(chart, Enumerable.Range(0, 10).Select(i => (PhoenixScore)(900000 + i)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.ScoreQuality90)
                && x.Detail != null && x.Detail.PeerCount == 10 && x.Detail.PeerBetterCount == 0
                && x.Detail.PeerPgCount == 0)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreQualityDoesNotFlagMidPackScores()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 910000);
        ctx.GivenCohort(chart, Enumerable.Range(0, 10)
            .Select(i => (PhoenixScore)(905000 + i * 10000)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AMidPackScoreStillRecordsWhereItStoodAmongPeers()
    {
        // The flag has a 90th-percentile bar; the percentile itself does not. The page
        // colours every row by it, and a row with no number reads as a bad score rather
        // than an unmeasured one.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 910000);
        ctx.GivenCohort(chart, Enumerable.Range(0, 10)
            .Select(i => (PhoenixScore)(905000 + i * 10000)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id
                && !x.Flags.HasFlag(HighlightFlags.ScoreQuality90)
                && x.Detail!.PeerPercentile != null
                && x.Detail.PeerCount == 10)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACoOpChartCarriesNoPeerStanding()
    {
        // Competitive cohorts have no co-op side, so there is nothing to measure this
        // against. The row still exists — folder completion is type-agnostic and a one-chart
        // folder is complete — but it carries no percentile, and the page renders it in plain
        // ink rather than inventing a band for it.
        //
        // Level 3 because that is what a co-op chart's level IS: the player count. Nothing in
        // Phoenix or Phoenix 2 carries a co-op above 5.
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id
                && x.Flags.HasFlag(HighlightFlags.FolderCompletion90)
                // Written out rather than ?. — Moq's predicate is an expression tree, and a
                // null-propagating operator cannot appear in one.
                && (x.Detail == null || x.Detail.PeerPercentile == null)
                && !x.Flags.HasFlag(HighlightFlags.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACoOpFolderNeverDebuts()
    {
        // BY DESIGN (owner, 2026-08-21), not an accident of the units. A mainline co-op chart
        // has no difficulty — its level slot holds the player count — so the debut floor, which
        // reads the overall competitive level for a type with no discipline of its own, is a bar
        // no co-op folder can clear. "First ever pass in the CoOp3 folder" would announce a party
        // size rather than an achievement. Default competitive levels here are 20; the chart is
        // a co-op x2, which is as real as co-op levels get.
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APlacementInsideTheOfficialBoardIsFlaggedWithItsDate()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var asOf = new DateTimeOffset(2026, 7, 27, 10, 30, 0, TimeSpan.Zero);
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 982140);
        ctx.OfficialPlacements.Setup(o => o.EstimatePlacements(It.IsAny<MixEnum>(), UserId,
                It.IsAny<IReadOnlyList<(Guid, int)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, OfficialPlacementReading>
                { [chart.Id] = new(42, 100, asOf) });

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id
                && x.Flags.HasFlag(HighlightFlags.OfficialBoardPlacement)
                && x.Detail!.OfficialPlace == 42
                && x.Detail.OfficialBoardDepth == 100
                && x.Detail.OfficialAsOf == asOf)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedPlacementReadStillLetsTheCaptureShip()
    {
        // The mirror is a different vertical on a weekly cadence — a bad snapshot read costs
        // this caption, never the capture around it.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 982140);
        ctx.GivenCohort(chart, new[] { (PhoenixScore)900000, (PhoenixScore)910000 });
        ctx.OfficialPlacements.Setup(o => o.EstimatePlacements(It.IsAny<MixEnum>(), UserId,
                It.IsAny<IReadOnlyList<(Guid, int)>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("snapshot unavailable"));

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Detail!.OfficialPlace == null)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreQualityDoesNotFlagAPerfectGameMostPeersAlsoHold()
    {
        // A PG is 100th percentile tie-inclusive, but a PG most of the cohort also holds
        // isn't noteworthy (owner call) — suppress it.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 1000000);
        // Six peers, four of them also PG (> half) — suppressed.
        ctx.GivenCohort(chart, new[] { 940000, 960000, 1000000, 1000000, 1000000, 1000000 }
            .Select(s => (PhoenixScore)s).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScoreQualityIsSuppressedMoreThanFiveLevelsBelowCompetitive()
    {
        // Owner call: the default player's competitive level is 20, so a level-14 back-fill
        // (more than 5 below) never earns a peer flag — even against a cohort it would top.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(14).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenCohort(chart, Enumerable.Range(0, 10).Select(i => (PhoenixScore)(900000 + i)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScoreQualityStillFlagsExactlyFiveLevelsBelowCompetitive()
    {
        // The cutoff is inclusive: level == competitive − 5 (20 − 5 = 15) still compares.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenCohort(chart, Enumerable.Range(0, 10).Select(i => (PhoenixScore)(900000 + i)).ToArray());

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.ScoreQuality90))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TouchedFoldersGetTheirStandingWritten()
    {
        // A 10-chart folder with four passes: 40% complete, averaging the four scores.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        foreach (var passed in others.Take(3)) ctx.GivenBest(passed, 930000);
        ctx.GivenBest(chart, 930000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.FolderLevels.Verify(f => f.Save(UserId,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().Folder == "S22"
                                                       && l.Single().Size == 10
                                                       && l.Single().Played == 4
                                                       && l.Single().AverageScore == 930000
                                                       && l.Single().CompletionPercent == 40),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FoldersTheBatchNeverTouchedAreLeftAlone()
    {
        var touched = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var untouched = new ChartBuilder().WithType(ChartType.Double).WithLevel(18).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(touched, untouched);
        ctx.GivenBest(touched, 930000);
        ctx.GivenBest(untouched, 990000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(touched)));

        ctx.FolderLevels.Verify(f => f.Save(UserId,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.All(x => x.Folder == "S22")),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABrokenScoreCountsTowardNeitherCompletionNorTheAverage()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var broken = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart, broken);
        ctx.GivenBest(chart, 930000);
        ctx.GivenBrokenBest(broken, 999999);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.FolderLevels.Verify(f => f.Save(UserId,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().Played == 1
                                                       && l.Single().AverageScore == 930000),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedFolderLevelWriteStillLetsTheCaptureShip()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 930000);
        ctx.FolderLevels.Setup(f => f.Save(It.IsAny<Guid>(), It.IsAny<IEnumerable<FolderLevelRecord>>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("projection is down"));
        var context = ctx.Context(NewPassEvent(chart));

        await ctx.Saga.Consume(context);

        // ComputeFlags runs inside one try/catch, so an unguarded projection failure would take
        // the flags and the published snapshot with it.
        Mock.Get(context).Verify(c => c.Publish(
            It.IsAny<ScoreHighlightsCapturedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<ScoreHighlightWrite>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderDebutFlagsTheFirstPassesInAFolder()
    {
        // Folder has 10 charts; this is the player's second-ever pass there.
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(others[0], 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.FolderDebut)
                && x.Detail != null && x.Detail.FolderDebutOrdinal == 2)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderDebutStopsAfterTheThirdPass()
    {
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        foreach (var passed in others.Take(3)) ctx.GivenBest(passed, 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FolderDebutIsSuppressedBelowFlooredCompetitiveLevel()
    {
        // Doubles competitive 24.5 floors to 24, so a first-ever pass in the D20 folder is a
        // back-fill of ground already held rather than a debut.
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCompetitive(singles: 24.5, doubles: 24.5);
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.Flags.HasFlag(HighlightFlags.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FolderDebutStillFlagsAtTheFlooredCompetitiveLevel()
    {
        // The cutoff is inclusive, and it floors: 24.5 competitive still debuts the D24 folder.
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(24).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(24).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCompetitive(singles: 24.5, doubles: 24.5);
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.FolderDebut)
                && x.Detail != null && x.Detail.FolderDebutOrdinal == 1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderDebutIsGatedByTheCompetitiveLevelForItsOwnType()
    {
        // A Singles folder gates on Singles competitive level, not the higher Doubles one —
        // matching PlayerHighlightPolicy, which reads the same helper.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCompetitive(singles: 18, doubles: 26);
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderCompletionIsNotGatedByCompetitiveLevel()
    {
        // Only the debut carries the floor (owner call): finishing an easy folder is the
        // achievement, so the pass that completes it stays marked however far below you play.
        // Two-chart S15 folder, one already passed — completion AND a free debut slot, so the
        // debut's absence here is the floor and nothing else.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var other = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var ctx = new HandlerContext();
        ctx.GivenCompetitive(singles: 24.5, doubles: 24.5);
        ctx.GivenCharts(other, chart);
        ctx.GivenBest(other, 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.FolderCompletion90)
                && !x.Flags.HasFlag(HighlightFlags.FolderDebut))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderCompletionFlagsPassesInNearlyCompleteFolders()
    {
        // 10-chart folder, 9 passed after this batch = 90%.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        foreach (var passed in others.Take(8)) ctx.GivenBest(passed, 920000);
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Highlights.Verify(h => h.UpsertFlags(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<ScoreHighlightWrite>>(w => w.Any(x =>
                x.ChartId == chart.Id && x.Flags.HasFlag(HighlightFlags.FolderCompletion90))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompletingAFolderFiresPassGradeAndPlateLamps()
    {
        // Two-chart D23 folder: one already passed, this pass completes it — the pass
        // lamp fires, plus the grade and plate floors that now exist.
        var chartA = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var chartB = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chartA, chartB);
        ctx.GivenBest(chartA, 981000);
        ctx.GivenBest(chartB, 970500);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chartB)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w =>
                w.Any(x => x.Kind == MilestoneKind.FolderPassLamp && x.Detail == "D23")
                && w.Any(x => x.Kind == MilestoneKind.FolderGradeLamp && x.Detail == "D23|S")
                && w.Any(x => x.Kind == MilestoneKind.FolderPlateLamp && x.Detail == "D23|FairGame")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FolderLampsRideThePublishedEvent()
    {
        // The Discord cards' milestone banner renders from the event — the lamps the
        // capture just persisted must travel with it, not require a racing read-back.
        var chartA = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var chartB = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chartA, chartB);
        ctx.GivenBest(chartA, 981000);
        ctx.GivenBest(chartB, 970500);
        var context = ctx.Context(NewPassEvent(chartB));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Milestones.Any(m => m.Kind == MilestoneKind.FolderPassLamp && m.Detail == "D23")
                && e.Milestones.Any(m => m.Kind == MilestoneKind.FolderGradeLamp && m.Detail == "D23|S")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WeeklyPlacementChangesBecomeMilestones()
    {
        // Weekly registration rides its own eligibility flow, so placements arrive as
        // the weekly vertical's progressed event — captured here as the gold rows the
        // Sessions page shows (SessionId deliberately null: no batch session exists).
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);

        await ctx.Saga.Consume(WeeklyContext(new UserWeeklyChartsProgressedEvent(UserId, chart.Id,
            1000000, "PerfectGame", false, 1, MixEnum.Phoenix)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w => w.Any(x =>
                x.Kind == MilestoneKind.WeeklyPlacement && x.NewValue == 1
                && x.Title == chart.Song.Name && x.Detail == chart.DifficultyString
                && x.SessionId == null)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<UserWeeklyChartsProgressedEvent> WeeklyContext(
        UserWeeklyChartsProgressedEvent message)
    {
        var ctx = new Mock<ConsumeContext<UserWeeklyChartsProgressedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Fact]
    public async Task RatingAndTitleStepsEnrichThePublishedSnapshot()
    {
        // The orchestration (revision 2): the rating and title steps run in-process
        // before the publish, so their milestones, the ⬆ improver flag, and the
        // per-title progress deltas all ride the one snapshot event.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenRatingStep(
            new[] { new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, sessionId, Now, 100, 150, null, null) },
            chart.Id);
        ctx.GivenTitleStep(
            new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, sessionId, Now, null, null,
                    "Intermediate Lv. 1", null)
            },
            new TitleProgressDelta("Expert Lv. 4", 0.82, 0.86));
        var context = ctx.Context(NewPassEvent(chart, sessionId));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Changes.Single().Flags.HasFlag(HighlightFlags.CompetitiveImprover)
                && e.Milestones.Any(m => m.Kind == MilestoneKind.PumbilityGain)
                && e.Milestones.Any(m => m.Kind == MilestoneKind.TitleCompleted)
                && e.TitleProgress.Single().Title == "Expert Lv. 4"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     ⚠ Load-bearing far beyond the Discord card. SessionRecoverySaga stamps a session's
    ///     "derived work finished" marker by consuming this event, so a session that produces no
    ///     flags, no milestones and no title movement must STILL publish — otherwise it is never
    ///     marked, looks interrupted forever, and the next process start replays it
    ///     (docs/design/import-restart-recovery.md §4.1).
    ///     <para>
    ///         An early return added to Consume for a quiet batch would be invisible in the UI and
    ///         would turn every ordinary session into a recovery candidate. This is the tripwire.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task ASnapshotWithNothingNoteworthyStillPublishes()
    {
        // Nothing set up: no charts, no bests, no rating or title output. ComputeFlags bails on
        // the first read and every step contributes nothing, which is as quiet as a batch gets.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        var context = ctx.Context(NewPassEvent(chart, sessionId));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e => e.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedStepShipsTheSnapshotWithoutItsSection()
    {
        // Failure isolation per step: the rating step blowing up costs the stats
        // section, never the announcement — the title step's output still ships.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.Mediator.Setup(m => m.Send(It.IsAny<PlayerRatingSaga.CaptureSessionStats>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("recalc boom"));
        ctx.GivenTitleStep(new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, null, Now, null, null, "Advanced Lv. 2", null)
        });
        var context = ctx.Context(NewPassEvent(chart));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Milestones.All(m => m.Kind != MilestoneKind.PumbilityGain)
                && e.Milestones.Any(m => m.Kind == MilestoneKind.TitleCompleted)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IncompleteFoldersFireNoLamps()
    {
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build();
        var others = Enumerable.Range(0, 9)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(23).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenCharts(others.Append(chart).ToArray());
        ctx.GivenBest(chart, 910000);

        await ctx.Saga.Consume(ctx.Context(NewPassEvent(chart)));

        ctx.Milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<PlayerMilestoneWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishesCapturedEventCarryingFlagsAndSession()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCharts(chart);
        ctx.GivenBest(chart, 950000);
        ctx.GivenTop50(chart.Id);
        var context = ctx.Context(NewPassEvent(chart, sessionId));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e => e.UserId == UserId
                                                     && e.SessionId == sessionId
                                                     && e.OccurredAt == Now
                                                     && e.Changes.Single().ChartId == chart.Id
                                                     && e.Changes.Single().Flags
                                                         .HasFlag(HighlightFlags.PumbilityTop50)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishesUnFlaggedWhenCaptureItselfFails()
    {
        // Capture must never cost the announcement.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.Charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));
        var context = ctx.Context(NewPassEvent(chart));

        await ctx.Saga.Consume(context);

        Mock.Get(context).Verify(c => c.Publish(
            It.Is<ScoreHighlightsCapturedEvent>(e =>
                e.Changes.Single().Flags == HighlightFlags.None),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Highlights.Verify(h => h.UpsertFlags(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<ScoreHighlightWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PlayerScoresUpdatedEvent NewPassesEvent(params Chart[] charts)
    {
        return PlayerScoresUpdatedEvent.Create(Now, UserId, MixEnum.Phoenix,
            charts.Select(c => new PlayerScoresUpdatedEvent.ScoreChange(c.Id, IsNewPass: true, OldScore: null,
                NewScore: 910000, Plate: "FairGame", IsBroken: false)).ToArray(), null);
    }

    private static PlayerScoresUpdatedEvent NewPassEvent(Chart chart, Guid? sessionId = null)
    {
        return PlayerScoresUpdatedEvent.Create(Now, UserId, MixEnum.Phoenix,
            new[]
            {
                new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, IsNewPass: true, OldScore: null,
                    NewScore: 910000, Plate: "FairGame", IsBroken: false)
            }, sessionId);
    }

    private sealed class HandlerContext
    {
        private readonly List<RecordedPhoenixScore> _bests = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerStatsReader> PlayerStats { get; } = new();
        public Mock<IScoreHighlightRepository> Highlights { get; } = new();
        public Mock<IPlayerMilestoneRepository> Milestones { get; } = new();
        public Mock<IPlayerFolderLevelRepository> FolderLevels { get; } = new();
        public Mock<IScoreAttemptReader> Attempts { get; } = new();
        public Mock<IOfficialPlacementReader> OfficialPlacements { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public HighlightCaptureSaga Saga { get; }

        public HandlerContext()
        {
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_bests);
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, double>());
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerStatsRecord(UserId, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 20, 20, 20));
            PlayerStats.Setup(p => p.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());
            FolderLevels.Setup(f => f.GetFolderLevels(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<FolderLevelRecord>());
            Attempts.Setup(a => a.GetSessionAttemptCounts(UserId, It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, int>());
            OfficialPlacements.Setup(o => o.EstimatePlacements(It.IsAny<MixEnum>(), UserId,
                    It.IsAny<IReadOnlyList<(Guid, int)>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, OfficialPlacementReading>());
            Saga = new HighlightCaptureSaga(Charts.Object, Scores.Object, PlayerStats.Object,
                Highlights.Object, Milestones.Object, FolderLevels.Object, Mediator.Object,
                new MemoryCache(new MemoryCacheOptions()), FakeDateTime.At(Now).Object,
                Attempts.Object, OfficialPlacements.Object,
                NullLogger<HighlightCaptureSaga>.Instance);
        }

        /// <summary>
        ///     Overrides the default 20/20/20 competitive levels. Singles and Doubles are set
        ///     separately because the folder gates read the one for the folder's own discipline.
        /// </summary>
        public void GivenCompetitive(double singles, double doubles, double overall = 20)
        {
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerStatsRecord(UserId, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
                    overall, singles, doubles));
        }

        public void GivenCharts(params Chart[] charts)
        {
            Charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
        }

        public void GivenBest(Chart chart, PhoenixScore score)
        {
            _bests.Add(new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.FairGame, false, Now));
        }

        public void GivenBrokenBest(Chart chart, PhoenixScore score)
        {
            _bests.Add(new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.FairGame, true, Now));
        }

        public void GivenTop50(params Guid[] chartIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(chartIds
                    .Select(id => new RecordedPhoenixScore(id, 950000, PhoenixPlate.FairGame, false, Now))
                    .ToArray());
        }

        public void GivenRatingStep(PlayerMilestoneRecord[] milestones, params Guid[] improverChartIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<PlayerRatingSaga.CaptureSessionStats>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerRatingSaga.SessionStatsResult(milestones, improverChartIds));
        }

        public void GivenTitleStep(PlayerMilestoneRecord[] milestones, params TitleProgressDelta[] progress)
        {
            Mediator.Setup(m => m.Send(It.IsAny<TitleSaga.CaptureSessionTitles>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TitleSaga.SessionTitlesResult(milestones, progress));
        }

        public void GivenCohort(Chart chart, PhoenixScore[] ascendingScores)
        {
            var players = new[] { Guid.NewGuid() };
            PlayerStats.Setup(p => p.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(players);
            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ascendingScores
                    .Select(s => (players[0],
                        new RecordedPhoenixScore(chart.Id, s, PhoenixPlate.FairGame, false, Now)))
                    .ToArray());
        }

        public ConsumeContext<PlayerScoresUpdatedEvent> Context(PlayerScoresUpdatedEvent message)
        {
            var ctx = new Mock<ConsumeContext<PlayerScoresUpdatedEvent>>();
            ctx.SetupGet(c => c.Message).Returns(message);
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return ctx.Object;
        }
    }
}
