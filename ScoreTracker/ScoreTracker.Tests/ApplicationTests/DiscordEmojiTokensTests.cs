using ScoreTracker.Data.Clients;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordEmojiTokensTests
{
    [Theory]
    [InlineData("#LETTERGRADE|SSSPlus#", "<:piu_sssplus:1238541135681552435>")]
    [InlineData("#LETTERGRADE|AA|False#", "<:piu_aa:1238540431457910840>")]
    [InlineData("#LETTERGRADE|AA|True#", "<:piu_aa_broken:1238540432699559936>")]
    [InlineData("#LETTERGRADE|f|false#", "<:piu_f:1238540776091422882>")]
    public void GradeTokensBecomeTheGradeEmoji(string token, string emoji)
    {
        Assert.Equal(emoji, DiscordEmojiTokens.Replace(token));
    }

    [Theory]
    [InlineData("#PLATE|PerfectGame#", "<:piu_pg:1238540780017025185>")]
    [InlineData("#PLATE|UltimateGame#", "<:piu_ug:1238541140429639781>")]
    [InlineData("#PLATE|RoughGame#", "<:piu_rg:1238540780402901033>")]
    public void PlateTokensBecomeThePlateEmoji(string token, string emoji)
    {
        Assert.Equal(emoji, DiscordEmojiTokens.Replace(token));
    }

    [Theory]
    [InlineData("#DIFFICULTY|S23#", "<:s23:1238568160060244079>")]
    [InlineData("#DIFFICULTY|d23#", "<:d23:1238568690711007292>")]
    [InlineData("#DIFFICULTY|SP12#", "<:s12:1238568299265134592>")]
    [InlineData("#DIFFICULTY|DP12#", "<:d12:1238569283471151154>")]
    [InlineData("#DIFFICULTY|CoOp3#", "<:coop3:1238582934185709621>")]
    public void DifficultyTokensBecomeTheStepballEmoji(string token, string emoji)
    {
        Assert.Equal(emoji, DiscordEmojiTokens.Replace(token));
    }

    [Theory]
    [InlineData("#MIX|Phoenix#", "<:phoenix_logo:1523325598171398164>")]
    [InlineData("#MIX|Phoenix2#", "<:phoenix2_logo:1523325648976875704>")]
    [InlineData("#MIX|XX#", "<:xx_logo:1523325684259356703>")]
    public void MixTokensBecomeTheMixLogo(string token, string emoji)
    {
        Assert.Equal(emoji, DiscordEmojiTokens.Replace(token));
    }

    [Fact]
    public void EmptyTokensDisappear()
    {
        Assert.Equal("a b c d", DiscordEmojiTokens.Replace("a#LETTERGRADE|# b#PLATE|# c#DIFFICULTY|# d#MIX|#"));
    }

    [Fact]
    public void TextAroundTokensIsKept()
    {
        Assert.Equal("<:s23:1238568160060244079> Gargoyle — **995,000** <:piu_pg:1238540780017025185>",
            DiscordEmojiTokens.Replace("#DIFFICULTY|S23# Gargoyle — **995,000** #PLATE|PerfectGame#"));
    }
}
