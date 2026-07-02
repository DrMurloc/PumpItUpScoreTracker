using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Ucs.Contracts.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using ScoreTracker.Ucs.Application;
using ScoreTracker.Ucs.Contracts;
using ScoreTracker.Ucs.Contracts.Commands;
using ScoreTracker.Ucs.Contracts.Queries;
using ScoreTracker.Ucs.Domain;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UcsSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RegisterUpdatesScoreThenPublishesFatPlacedEvent()
    {
        var ctx = new SagaContext();
        var chartId = Guid.NewGuid();
        var chart = ctx.GivenUcsChart(chartId, artist: "StepMaker", songName: "Test Song");

        await ctx.Saga.Handle(
            new RegisterUcsEntryCommand(chartId, PhoenixScore.From(950000), PhoenixPlate.SuperbGame, IsBroken: false,
                VideoPath: null, ImagePath: null),
            CancellationToken.None);

        ctx.Ucs.Verify(u => u.UpdateScore(chartId, ctx.UserId, PhoenixScore.From(950000), PhoenixPlate.SuperbGame,
            false, null, null, It.IsAny<CancellationToken>()), Times.Once);
        // The event carries the placement facts so consumers (Community Discord posts,
        // future webhooks) never reach back into UCS storage.
        ctx.Bus.Verify(b => b.Publish(
            It.Is<UcsLeaderboardPlacedEvent>(e => e.UserId == ctx.UserId
                                                  && e.ChartId == chartId
                                                  && e.OccurredAt == Now
                                                  && e.SchemaVersion == UcsLeaderboardPlacedEvent.CurrentSchemaVersion
                                                  && e.EventId != Guid.Empty
                                                  && e.Score == 950000
                                                  && e.Plate == "SuperbGame"
                                                  && !e.IsBroken
                                                  && e.Artist == "StepMaker"
                                                  && e.SongName == "Test Song"
                                                  && e.Difficulty == chart.Chart.DifficultyString),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PassesVideoAndImagePathsThrough()
    {
        var ctx = new SagaContext();
        var chartId = Guid.NewGuid();
        ctx.GivenUcsChart(chartId, artist: "StepMaker", songName: "Test Song");
        var video = new Uri("https://example.invalid/v.mp4");
        var image = new Uri("https://example.invalid/i.png");

        await ctx.Saga.Handle(
            new RegisterUcsEntryCommand(chartId, PhoenixScore.From(800000), PhoenixPlate.MarvelousGame, IsBroken: true,
                VideoPath: video, ImagePath: image),
            CancellationToken.None);

        ctx.Ucs.Verify(u => u.UpdateScore(chartId, ctx.UserId, PhoenixScore.From(800000), PhoenixPlate.MarvelousGame,
            true, video, image, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TagCommandsScopeToTheCurrentUser()
    {
        var ctx = new SagaContext();
        var chartId = Guid.NewGuid();

        await ctx.Saga.Handle(new AddUcsChartTagCommand(chartId, Name.From("Stamina")), CancellationToken.None);
        await ctx.Saga.Handle(new DeleteUcsChartTagCommand(chartId, Name.From("Stamina")), CancellationToken.None);

        ctx.Ucs.Verify(u => u.AddChartTag(chartId, ctx.UserId, Name.From("Stamina"),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Ucs.Verify(u => u.DeleteChartTag(chartId, ctx.UserId, Name.From("Stamina"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MyTagQueriesScopeToTheCurrentUser()
    {
        var ctx = new SagaContext();
        var chartId = Guid.NewGuid();
        ctx.Ucs.Setup(u => u.GetMyTags(chartId, ctx.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Name.From("Drills") });
        ctx.Ucs.Setup(u => u.GetAllMyTags(ctx.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserChartTag(chartId, ctx.UserId, "Drills") });

        var myTags = await ctx.Saga.Handle(new GetMyUcsChartTagsQuery(chartId), CancellationToken.None);
        var allMyTags = await ctx.Saga.Handle(new GetAllMyUcsChartTagsQuery(), CancellationToken.None);

        Assert.Contains(Name.From("Drills"), myTags);
        Assert.Contains(allMyTags, t => t.ChartId == chartId);
    }

    private sealed class SagaContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IBus> Bus { get; } = new();
        public Mock<IUcsRepository> Ucs { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public UcsSaga Saga { get; }

        public SagaContext()
        {
            CurrentUser.SetupGet(c => c.User).Returns(new UserBuilder().WithId(UserId).Build());
            Saga = new UcsSaga(Bus.Object, Ucs.Object, CurrentUser.Object, FakeDateTime.At(Now).Object);
        }

        public UcsChart GivenUcsChart(Guid chartId, string artist, string songName)
        {
            var chart = new UcsChart(PiuGameId: 1234,
                new ChartBuilder().WithId(chartId).WithSongName(songName).Build(),
                Uploader: Name.From("Uploader"), Artist: Name.From(artist), Description: "A chart",
                SubmissionCount: 0);
            Ucs.Setup(u => u.GetUcsCharts(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { chart });
            return chart;
        }
    }
}
