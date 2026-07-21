using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.PlayerProgress.Domain.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RecapBadgesTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RecordedPhoenixScore Record(int? score, bool isBroken = false, PhoenixPlate? plate = null)
    {
        return new RecordedPhoenixScore(Guid.NewGuid(), score is null ? null : (PhoenixScore)score.Value,
            plate, isBroken, RecordedAt);
    }

    [Theory]
    [InlineData(100, 200, null)]
    [InlineData(101, 200, RecapBadge.TitleHunter)]
    [InlineData(150, 200, RecapBadge.TitleHunter)]
    [InlineData(151, 200, RecapBadge.TitleCollector)]
    [InlineData(181, 200, RecapBadge.TitleMaster)]
    [InlineData(191, 200, RecapBadge.LeaveSomeTitlesForTheRestOfUs)]
    [InlineData(5, 0, null)]
    public void TitleCollectionLadderUsesStrictSharesOfTheFullList(int earned, int total, RecapBadge? expected)
    {
        Assert.Equal(expected, RecapBadges.CollectionBadge(earned, total));
    }

    [Fact]
    public void CoOpFoldersDoNotCountTowardCompletionist()
    {
        var folders = new[]
        {
            new RecapFolder(ChartType.CoOp, 2, 10, 10),
            new RecapFolder(ChartType.Single, 20, 10, 10)
        };

        Assert.Equal(1, RecapBadges.CountFoldersOver90(folders));
    }

    [Fact]
    public void NinetyPercentFolderBoundaryIsInclusive()
    {
        var folders = new[]
        {
            new RecapFolder(ChartType.Single, 20, 10, 9),
            new RecapFolder(ChartType.Double, 21, 10, 8),
            new RecapFolder(ChartType.Double, 22, 0, 0)
        };

        Assert.Equal(1, RecapBadges.CountFoldersOver90(folders));
    }

    [Theory]
    [InlineData(4, null)]
    [InlineData(5, RecapBadge.Completionist)]
    [InlineData(10, RecapBadge.CompletionistPlus)]
    [InlineData(20, RecapBadge.CompletionistSupreme)]
    [InlineData(30, RecapBadge.CompletionistUltra)]
    [InlineData(40, RecapBadge.YouKnowPumpItUpDoesntDoLamps)]
    public void CompletionistLadderStepsAtCalibratedFolderCounts(int foldersOver90, RecapBadge? expected)
    {
        Assert.Equal(expected, RecapBadges.CompletionistBadge(foldersOver90));
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(10, 5, null)]
    [InlineData(10, 6, RecapBadge.Socialite)]
    [InlineData(100, 76, RecapBadge.ClearlyHasFriends)]
    [InlineData(100, 91, RecapBadge.FriendshipIsMagic)]
    [InlineData(10, 10, RecapBadge.IHopeYouHeldHandsOnCanonD)]
    public void CoOpLadderStepsAtCalibratedShares(int total, int passed, RecapBadge? expected)
    {
        Assert.Equal(expected, RecapBadges.CoOpBadge(total, passed));
    }

    [Theory]
    [InlineData("BanYa", true)]
    [InlineData("Banya Production", true)]
    [InlineData("YAHPP", true)]
    [InlineData("BanYa & DM Ashura", true)]
    [InlineData("msgoon", false)]
    [InlineData("SHK", false)]
    public void BanYaArtistMatchingCoversTheCalibratedSpellings(string artist, bool expected)
    {
        Assert.Equal(expected, RecapBadges.IsBanYaArtist(Name.From(artist)));
    }

    [Fact]
    public void MissingArtistIsNotBanYa()
    {
        Assert.False(RecapBadges.IsBanYaArtist(null));
    }

    [Theory]
    [InlineData(162, 81, null)]
    [InlineData(162, 82, RecapBadge.BanYaLover)]
    [InlineData(0, 0, null)]
    public void BanYaLoverNeedsAStrictMajorityPassed(int total, int passed, RecapBadge? expected)
    {
        Assert.Equal(expected, RecapBadges.BanYaBadge(total, passed));
    }

    [Fact]
    public void BigFeetNeedsAnUnbrokenSssPlusOnTheChart()
    {
        Assert.False(RecapBadges.EarnsBigFeet(null, MixEnum.Phoenix));
        Assert.False(RecapBadges.EarnsBigFeet(Record(999_000, isBroken: true), MixEnum.Phoenix));
        Assert.False(RecapBadges.EarnsBigFeet(Record(994_999), MixEnum.Phoenix));
        Assert.True(RecapBadges.EarnsBigFeet(Record(995_000), MixEnum.Phoenix));
        Assert.True(RecapBadges.EarnsBigFeet(Record(1_000_000, plate: PhoenixPlate.PerfectGame), MixEnum.Phoenix));
    }

    [Fact]
    public void GrandMashterNeedsMashGradePassesOnMoreThanThreeQuartersOfTheFolder()
    {
        var twentySeven = Enumerable.Range(0, 27).Select(_ => Record(940_000)).ToArray();
        var twentyEight = Enumerable.Range(0, 28).Select(_ => Record(940_000)).ToArray();

        Assert.False(RecapBadges.EarnsGrandMashter(twentySeven, 36, MixEnum.Phoenix));
        Assert.True(RecapBadges.EarnsGrandMashter(twentyEight, 36, MixEnum.Phoenix));
    }

    [Fact]
    public void StrayHighGradesNeitherCountNorDisqualifyGrandMashter()
    {
        var mashWithAaaPasses = Enumerable.Range(0, 28).Select(_ => Record(949_999))
            .Concat(Enumerable.Range(0, 5).Select(_ => Record(985_000)))
            .ToArray();

        Assert.True(RecapBadges.EarnsGrandMashter(mashWithAaaPasses, 36, MixEnum.Phoenix));
    }

    [Fact]
    public void ScorelessAndBrokenRecordsDoNotCountAsMashPasses()
    {
        var records = Enumerable.Range(0, 27).Select(_ => Record(940_000))
            .Append(Record(null))
            .Append(Record(940_000, isBroken: true))
            .ToArray();

        Assert.False(RecapBadges.EarnsGrandMashter(records, 36, MixEnum.Phoenix));
    }

    [Fact]
    public void GrandMashterNeedsAFolderToMash()
    {
        Assert.False(RecapBadges.EarnsGrandMashter(Array.Empty<RecordedPhoenixScore>(), 0, MixEnum.Phoenix));
    }

    [Fact]
    public void NowYouCanPlayTheGameNeedsADoubles28Pass()
    {
        Assert.False(RecapBadges.EarnsNowYouCanPlayTheGame(Array.Empty<DifficultyLevel>()));
        Assert.False(RecapBadges.EarnsNowYouCanPlayTheGame(new DifficultyLevel[] { 27 }));
        Assert.True(RecapBadges.EarnsNowYouCanPlayTheGame(new DifficultyLevel[] { 28 }));
        Assert.True(RecapBadges.EarnsNowYouCanPlayTheGame(new DifficultyLevel[] { 26, 28 }));
    }

    [Theory]
    [InlineData("DULKI #2827", true)]
    [InlineData("DULKI #2828", false)]
    [InlineData("dulki #2827", false)]
    [InlineData(null, false)]
    public void DoveIsAnExactCaseSensitiveTagMatch(string? gameTag, bool expected)
    {
        Assert.Equal(expected, RecapBadges.EarnsDove(gameTag));
    }

    [Fact]
    public void SnowflakeIsTheRarestEarnedTitleUnderOnePercent()
    {
        var holders = new Dictionary<Name, int>
        {
            [Name.From("Common Title")] = 300,
            [Name.From("Rare Title")] = 5
        };

        var snowflake = RecapBadges.Snowflake(
            new[] { Name.From("Common Title"), Name.From("Rare Title") }, holders, 1000);

        Assert.NotNull(snowflake);
        Assert.Equal(Name.From("Rare Title"), snowflake!.Title);
        Assert.Equal(.005, snowflake.HolderShare, 5);
    }

    [Fact]
    public void ExactlyOnePercentIsNotASnowflake()
    {
        var holders = new Dictionary<Name, int> { [Name.From("Title")] = 10 };

        Assert.Null(RecapBadges.Snowflake(new[] { Name.From("Title") }, holders, 1000));
    }

    [Fact]
    public void TitlesMissingFromTheAggregationNeverMintSnowflakes()
    {
        var snowflake = RecapBadges.Snowflake(
            new[] { Name.From("Unaggregated") }, new Dictionary<Name, int>(), 1000);

        Assert.Null(snowflake);
    }

    [Fact]
    public void NoTitledUsersMeansNoRarityData()
    {
        var holders = new Dictionary<Name, int> { [Name.From("Title")] = 1 };

        Assert.Empty(RecapBadges.RarestTitles(new[] { Name.From("Title") }, holders, 0, 3));
    }

    [Fact]
    public void RarestTitlesOrderByShareAndCapTheCount()
    {
        var holders = new Dictionary<Name, int>
        {
            [Name.From("A")] = 500,
            [Name.From("B")] = 5,
            [Name.From("C")] = 20,
            [Name.From("D")] = 1
        };
        var earned = new[] { Name.From("A"), Name.From("B"), Name.From("C"), Name.From("D") };

        var rarest = RecapBadges.RarestTitles(earned, holders, 1000, 3);

        Assert.Equal(new[] { Name.From("D"), Name.From("B"), Name.From("C") },
            rarest.Select(r => r.Title).ToArray());
    }

    [Fact]
    public void PlateCabinetCountsUnbrokenPlatedRecordsOnly()
    {
        var records = new[]
        {
            Record(1_000_000, plate: PhoenixPlate.PerfectGame),
            Record(1_000_000, plate: PhoenixPlate.PerfectGame),
            Record(990_000, plate: PhoenixPlate.UltimateGame),
            Record(980_000, isBroken: true, plate: PhoenixPlate.PerfectGame),
            Record(970_000)
        };

        var cabinet = RecapBadges.PlateCabinet(records);

        Assert.Equal(2, cabinet[PhoenixPlate.PerfectGame]);
        Assert.Equal(1, cabinet[PhoenixPlate.UltimateGame]);
        Assert.Equal(2, cabinet.Count);
    }
}
