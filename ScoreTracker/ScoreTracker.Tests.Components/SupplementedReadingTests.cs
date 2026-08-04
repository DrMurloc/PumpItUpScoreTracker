using System;
using System.Collections.Generic;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages.OfficialLeaderboards;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     How the supplemented reading renders (docs/design/supplemented-leaderboards.md §8): the
///     marker that has to survive alongside the you/community glow, the note Popularity shows
///     because it cannot honour the switch, and the flag actually reaching the queries.
/// </summary>
public sealed class SupplementedReadingTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private static readonly DateTimeOffset Week = new(2026, 8, 2, 17, 0, 0, TimeSpan.Zero);
    private static readonly Guid MyUserId = Guid.Parse("0f5f1d3f-1111-4000-8000-000000000001");

    public SupplementedReadingTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        // The glow reader short-circuits when signed out; the signed-in fact below needs both.
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(Mock.Of<IAdminNotificationClient>());
        Services.AddScoped<CommunityGlowReader>();
        this.RenderInteractive();
    }

    private static OfficialPlayerRecord Player(int id, string name, bool supplemented, Guid? userId = null) =>
        new(id, name, null, userId, supplemented);

    private void SetRankings(params OfficialRankingRecord[] rows) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialRankingsRecord(Week, true, rows));

    [Fact]
    public void ASupplementedRowWearsTheRailAndTheChip()
    {
        SetRankings(new OfficialRankingRecord(1, null, Player(1, "HHARDDOG#3706", true), 1014.02m, 12, null));

        var cut = RenderComponent<HubRankings>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Supplemented, true));

        Assert.Contains("olb-row-supp", cut.Markup);
        Assert.Contains("olb-supp-chip", cut.Markup);
    }

    [Fact]
    public void AnOfficialRowWearsNeither()
    {
        SetRankings(new OfficialRankingRecord(1, null, Player(1, "SUNNY#1", false), 1043.87m, 40, null));

        var cut = RenderComponent<HubRankings>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Supplemented, true));

        Assert.DoesNotContain("olb-row-supp", cut.Markup);
        Assert.DoesNotContain("olb-supp-chip", cut.Markup);
    }

    /// <summary>
    ///     The reason the marker is an edge and not a background: a row can be supplemented and
    ///     be yours at the same time, and losing either fact would be wrong.
    /// </summary>
    [Fact]
    public void YourOwnSupplementedRowKeepsBothTheGlowAndTheRail()
    {
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(u => u.User).Returns(new ScoreTracker.Domain.Models.User(MyUserId,
            ScoreTracker.SharedKernel.ValueTypes.Name.From("me"), true, null,
            new Uri("https://piu.test/a.png"), null));
        SetRankings(new OfficialRankingRecord(1, null, Player(1, "ME#1", true, MyUserId), 1000m, 3, null));

        var cut = RenderComponent<HubRankings>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Supplemented, true));

        Assert.Contains("olb-row-me", cut.Markup);
        Assert.Contains("olb-row-supp", cut.Markup);
    }

    [Fact]
    public void TheReadingReachesTheQuery()
    {
        SetRankings(new OfficialRankingRecord(1, null, Player(1, "SUNNY#1", false), 1043.87m, 40, null));

        RenderComponent<HubRankings>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Supplemented, true));

        _mediator.Verify(m => m.Send(It.Is<GetOfficialRankingsQuery>(q => q.Supplemented),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void PopularitySaysWhyItCannotBeSupplemented()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPopularityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OfficialPopularityRecord>)new[]
            {
                new OfficialPopularityRecord(Guid.NewGuid(), 1, null, Array.Empty<int>())
            });

        var cut = RenderComponent<HubPopularity>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Supplemented, true));

        Assert.Contains("olb-reading-note", cut.Markup);
        Assert.Contains("ranked on full play data", cut.Markup);
    }

    [Fact]
    public void PopularityStaysQuietInTheOfficialReading()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPopularityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OfficialPopularityRecord>)new[]
            {
                new OfficialPopularityRecord(Guid.NewGuid(), 1, null, Array.Empty<int>())
            });

        var cut = RenderComponent<HubPopularity>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Supplemented, false));

        Assert.DoesNotContain("olb-reading-note", cut.Markup);
    }
}
