using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class EmojiTokensTests
{
    [Theory]
    [InlineData(MixEnum.Rise, "#LETTERGRADE|Rise/SS|False#")]
    [InlineData(MixEnum.Phoenix2, "#LETTERGRADE|SS|False#")]
    [InlineData(MixEnum.Phoenix, "#LETTERGRADE|SS|False#")]
    [InlineData(MixEnum.RiseArcade, "#LETTERGRADE|SS|False#")]
    public void AGradeTokenNamesTheMixsOwnArtSetWhenItHasOne(MixEnum mix, string token)
    {
        Assert.Equal(token, EmojiTokens.LetterGrade(mix, PhoenixLetterGrade.SS, false));
    }

    [Fact]
    public void ABrokenGradeSaysSo()
    {
        Assert.Equal("#LETTERGRADE|Rise/A|True#", EmojiTokens.LetterGrade(MixEnum.Rise, PhoenixLetterGrade.A, true));
    }

    [Theory]
    [InlineData(MixEnum.Rise, "#PLATE|Rise/UltimateGame#")]
    [InlineData(MixEnum.Phoenix2, "#PLATE|UltimateGame#")]
    public void AnAwardTokenNamesTheMixsOwnArtSetWhenItHasOne(MixEnum mix, string token)
    {
        Assert.Equal(token, EmojiTokens.Plate(mix, PhoenixPlate.UltimateGame));
        Assert.Equal(token, EmojiTokens.PlateNamed(mix, "UltimateGame"));
    }

    [Fact]
    public void NoAwardWritesTheEmptyToken()
    {
        Assert.Equal("#PLATE|#", EmojiTokens.Plate(MixEnum.Rise, null));
    }
}
