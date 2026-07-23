using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SessionFeedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();

    [Fact]
    public async Task NonPublicPlayersReadAsAnEmptyPageToAnonymousViewers()
    {
        // Defense in depth behind the page's redirect-to-home.
        var ctx = new HandlerContext(isPublic: false);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        Assert.Equal(0, page.TotalGroups);
        Assert.Empty(page.Groups);
        ctx.Journal.Verify(j => j.GetSessionGroups(It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonPublicPlayersReadAsAnEmptyPageToOtherPlayers()
    {
        var ctx = new HandlerContext(isPublic: false, viewerId: Guid.NewGuid());

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        Assert.Equal(0, page.TotalGroups);
        Assert.Empty(page.Groups);
        ctx.Journal.Verify(j => j.GetSessionGroups(It.IsAny<Guid>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonPublicPlayersStillReadTheirOwnSessions()
    {
        var ctx = new HandlerContext(isPublic: false, viewerId: UserId);
        var rows = new[] { Entry(Now, 950000) };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        Assert.Equal(1, page.TotalGroups);
        Assert.Single(page.Groups.Single().Rows);
    }

    [Fact]
    public async Task RowsClassifyAgainstTheChartsPriorJournalState()
    {
        // One chart's story: broken backfill -> first pass -> upscore -> legacy no-op.
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddDays(-3), 800000, isBroken: true, source: "backfill"),
            Entry(Now.AddDays(-2), 900000),
            Entry(Now.AddDays(-1), 950000),
            Entry(Now, 950000)
        };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var byTime = page.Groups.Single().Rows.OrderBy(r => r.OccurredAt).ToArray();
        Assert.Equal(ScoreEventClassification.Break, byTime[0].Classification);
        Assert.Equal(ScoreEventClassification.NewPass, byTime[1].Classification);
        Assert.Equal(ScoreEventClassification.Upscore, byTime[2].Classification);
        Assert.Equal(900000, byTime[2].PreviousBest);
        Assert.Equal(ScoreEventClassification.Played, byTime[3].Classification);
    }

    [Fact]
    public async Task FirstPassOnANewerVersionIsANewPassNotACrossMixUpscore()
    {
        // Phoenix and Phoenix 2 share the 1M scale: a first Phoenix 2 pass must not be
        // mislabeled an Upscore over the Phoenix best. It reads as a NewPass carrying the
        // earlier-version best (981199) for the "+40 from Phoenix" note.
        var ctx = new HandlerContext();
        var phoenixBest = Entry(Now.AddDays(-5), 981199, mix: MixEnum.Phoenix);
        var firstPhoenix2 = Entry(Now, 981239, mix: MixEnum.Phoenix2);
        var rows = new[] { phoenixBest, firstPhoenix2 };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix2, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var latest = page.Groups.Single().Rows.OrderBy(r => r.OccurredAt).Last();
        Assert.Equal(ScoreEventClassification.NewPass, latest.Classification);
        Assert.Null(latest.PreviousBest);
        Assert.Equal(981199, latest.PreviousMixBest);
        Assert.Equal(MixEnum.Phoenix, latest.PreviousMix);
    }

    [Fact]
    public async Task UpscoresAreScopedToTheSameMix()
    {
        // A prior Phoenix 2 best is what a later Phoenix 2 row upscores against — the Phoenix
        // best is carried, not compared.
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddDays(-5), 990000, mix: MixEnum.Phoenix),
            Entry(Now.AddDays(-1), 970000, mix: MixEnum.Phoenix2),
            Entry(Now, 975000, mix: MixEnum.Phoenix2)
        };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix2, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var latest = page.Groups.Single().Rows.OrderBy(r => r.OccurredAt).Last();
        Assert.Equal(ScoreEventClassification.Upscore, latest.Classification);
        Assert.Equal(970000, latest.PreviousBest);
        Assert.Null(latest.PreviousMixBest);
    }

    [Fact]
    public async Task PlateOnlyImprovementsClassifyAsUpscores()
    {
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddDays(-1), 950000, plate: PhoenixPlate.FairGame),
            Entry(Now, 950000, plate: PhoenixPlate.SuperbGame)
        };
        ctx.GivenGroups(new JournalSessionRows(null, DateOnly.FromDateTime(Now.Date), MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var latest = page.Groups.Single().Rows.OrderBy(r => r.OccurredAt).Last();
        Assert.Equal(ScoreEventClassification.Upscore, latest.Classification);
    }

    [Fact]
    public async Task GroupsCarryDominantSourceAndTimeSpan()
    {
        var sessionId = Guid.NewGuid();
        var ctx = new HandlerContext();
        var rows = new[]
        {
            Entry(Now.AddMinutes(-90), 900000, sessionId: sessionId, source: "officialImport"),
            Entry(Now, 950000, sessionId: sessionId, source: "officialImport")
        };
        ctx.GivenGroups(new JournalSessionRows(sessionId, null, MixEnum.Phoenix, rows));
        ctx.GivenHistories(rows);

        var page = await ctx.Handler.Handle(new GetRecentSessionsQuery(UserId),
            CancellationToken.None);

        var group = page.Groups.Single();
        Assert.Equal(sessionId, group.SessionId);
        Assert.Equal("officialImport", group.Source);
        Assert.Equal(Now.AddMinutes(-90), group.Start);
        Assert.Equal(Now, group.End);
        // Newest first within the group.
        Assert.Equal(Now, group.Rows[0].OccurredAt);
    }

    private static ScoreJournalEntry Entry(DateTimeOffset at, int score, bool isBroken = false,
        string source = "manual", PhoenixPlate? plate = PhoenixPlate.FairGame, Guid? sessionId = null,
        MixEnum mix = MixEnum.Phoenix)
    {
        return new ScoreJournalEntry(at, source, UserId, ChartId, score, plate, isBroken, mix,
            sessionId);
    }

    private sealed class HandlerContext
    {
        public Mock<IScoreJournalRepository> Journal { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public SessionFeedHandler Handler { get; }

        public HandlerContext(bool isPublic = true, Guid? viewerId = null)
        {
            Users.Setup(u => u.GetUser(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserBuilder().WithId(UserId).WithIsPublic(isPublic).Build());
            if (viewerId is { } viewer)
            {
                CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
                CurrentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(viewer).Build());
            }
            Handler = new SessionFeedHandler(Journal.Object, Users.Object, CurrentUser.Object);
        }

        public void GivenGroups(params JournalSessionRows[] groups)
        {
            Journal.Setup(j => j.GetSessionGroups(UserId, It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((groups.Length, groups));
        }

        public void GivenHistories(IEnumerable<ScoreJournalEntry> rows)
        {
            Journal.Setup(j => j.GetChartHistories(UserId, It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(rows.ToArray());
        }
    }
}
