using System;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class PhoenixTitleTests
{
    // PhoenixBasicTitle is the lightest concrete subclass and inherits all PhoenixTitle base
    // behavior (default CompletionProgress and PopulatesFromDatabase) without overriding it.

    [Fact]
    public void DefaultCompletionProgressIsZero()
    {
        var title = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome to Phoenix");
        var chart = new ChartBuilder().WithLevel(20).Build();
        var attempt = new RecordedPhoenixScore(Guid.NewGuid(), 990000, PhoenixPlate.PerfectGame, false,
            DateTimeOffset.UtcNow);

        Assert.Equal(0, title.CompletionProgress(chart, attempt));
    }

    [Fact]
    public void DefaultPopulatesFromDatabaseIsTrue()
    {
        var title = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome");

        Assert.True(title.PopulatesFromDatabase);
    }

    [Fact]
    public void TwoArgConstructorDefaultsCategoryToMisc()
    {
        var title = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome");

        Assert.Equal("Misc.", (string)title.Category);
    }

    [Fact]
    public void ThreeArgConstructorAcceptsExplicitCategory()
    {
        var title = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome", Name.From("Onboarding"));

        Assert.Equal("Onboarding", (string)title.Category);
    }

    [Fact]
    public void BasicTitleHasZeroCompletionRequired()
    {
        var title = new PhoenixBasicTitle(Name.From("Welcome"), "Welcome");

        Assert.Equal(0, title.CompletionRequired);
    }
}
