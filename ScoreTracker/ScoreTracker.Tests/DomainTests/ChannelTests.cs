using System;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChannelTests
{
    [Fact]
    public void TheSixChannelsComeInTheGamesOrder()
    {
        Assert.Equal(
            new[] { Channel.Original, Channel.KPop, Channel.WorldMusic, Channel.JMusic, Channel.Xross, Channel.RiseVol1 },
            Enum.GetValues<Channel>());
    }

    [Theory]
    [InlineData(Channel.Original, "Original")]
    [InlineData(Channel.KPop, "K-Pop")]
    [InlineData(Channel.WorldMusic, "World Music")]
    [InlineData(Channel.JMusic, "J-Music")]
    [InlineData(Channel.Xross, "Xross")]
    [InlineData(Channel.RiseVol1, "R!SE Vol.1")]
    public void EveryChannelPrintsItsDisplayName(Channel channel, string expected)
    {
        Assert.Equal(expected, channel.GetName());
    }

    [Fact]
    public void TheLabelKeyIsPrefixedSoOriginalNeverCollidesWithTheTranslationKey()
    {
        Assert.Equal("Channel: K-Pop", Channel.KPop.GetLabelKey());
        Assert.Equal("Channel: Original", Channel.Original.GetLabelKey());
    }

    [Theory]
    [InlineData("KPop", Channel.KPop)]
    [InlineData("kpop", Channel.KPop)]
    [InlineData(" WorldMusic ", Channel.WorldMusic)]
    [InlineData("Xross", Channel.Xross)]
    [InlineData("risevol1", Channel.RiseVol1)]
    public void TheTokenIsTheEnumNameCaseInsensitively(string token, Channel expected)
    {
        Assert.True(ChannelHelperMethods.TryParse(token, out var channel));
        Assert.Equal(expected, channel);
    }

    [Theory]
    [InlineData("K-Pop")]
    [InlineData("World Music")]
    [InlineData("R!SE Vol.1")]
    [InlineData("7")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData(null)]
    public void ADisplayNameANumberOrNothingIsNotAToken(string? value)
    {
        Assert.False(ChannelHelperMethods.TryParse(value, out _));
    }

    [Fact]
    public void RandomSettingsDrawFromEveryChannelUntilOneIsPicked()
    {
        var settings = new RandomSettings();

        Assert.Empty(settings.Channels);

        settings.Channels.Add(Channel.KPop);

        Assert.Equal(new[] { Channel.KPop }, settings.Channels);
    }
}
