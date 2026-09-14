using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Catalog.Application;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SongChannelHandlersTests
{
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ISongMixRepository> _songMixes = new();

    [Fact]
    public async Task ChannelsComeBackInTheGamesOrderCountingSongsOnceAndChartsEach()
    {
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chart[]
            {
                new ChartBuilder().WithSongName("Nostalgia").WithLevel(21).WithChannel(Channel.KPop),
                new ChartBuilder().WithSongName("Nostalgia").WithLevel(17).WithChannel(Channel.KPop),
                new ChartBuilder().WithSongName("Yoropiku Pikuyoro !").WithChannel(Channel.JMusic),
                new ChartBuilder().WithSongName("Ghroth").WithChannel(Channel.Original),
                new ChartBuilder().WithSongName("Unknown Song")
            });

        var result = await new GetMixChannelsHandler(_charts.Object)
            .Handle(new GetMixChannelsQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.Equal(new[] { Channel.Original, Channel.KPop, Channel.JMusic }, result.Select(r => r.Channel));
        Assert.Equal(new[] { 1, 1, 1 }, result.Select(r => r.SongCount));
        Assert.Equal(new[] { 1, 2, 1 }, result.Select(r => r.ChartCount));
        Assert.All(result, r => Assert.Equal(MixEnum.Phoenix, r.Mix));
    }

    [Fact]
    public async Task AMixWhoseSongsHaveNoChannelOffersNone()
    {
        _charts.Setup(c => c.GetCharts(MixEnum.Prime, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Chart[] { new ChartBuilder().WithMix(MixEnum.Prime) });

        var result = await new GetMixChannelsHandler(_charts.Object)
            .Handle(new GetMixChannelsQuery(MixEnum.Prime), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SettingAChannelWritesTheSongsRowOnThatMix()
    {
        var songId = Guid.NewGuid();

        await new SetSongChannelHandler(_songMixes.Object)
            .Handle(new SetSongChannelCommand(MixEnum.Phoenix2, songId, Channel.WorldMusic), CancellationToken.None);

        _songMixes.Verify(s => s.SetChannel(MixEnum.Phoenix2, songId, Channel.WorldMusic, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
