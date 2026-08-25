using ScoreTracker.Domain.Records;
using ScoreTracker.Translations.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TranslationBudgetTests
{
    [Fact]
    public void AQuietMonthAllowsTheFullNightlyCount()
    {
        Assert.Equal(50, TranslationBudget.Allowance(30m, 0.50m, 0.10m, 0.016m, 50));
    }

    [Fact]
    public void HeadroomBoundsTheTakeWhenItIsTighterThanTheCount()
    {
        // $0.10 of headroom at $0.016 a text is six texts, floored.
        Assert.Equal(6, TranslationBudget.Allowance(30m, 29.80m, 0.10m, 0.016m, 50));
    }

    [Fact]
    public void ABlownCeilingParksTheNight()
    {
        Assert.Equal(0, TranslationBudget.Allowance(30m, 30m, 0m, 0.016m, 50));
        Assert.Equal(0, TranslationBudget.Allowance(30m, 25m, 6m, 0.016m, 50));
    }

    [Fact]
    public void DegenerateConfigurationAllowsNothing()
    {
        Assert.Equal(0, TranslationBudget.Allowance(30m, 0m, 0m, 0m, 50));
        Assert.Equal(0, TranslationBudget.Allowance(30m, 0m, 0m, 0.016m, 0));
    }

    [Fact]
    public void CostPricesTheFourKindsOfTokenAtTheirOwnRates()
    {
        // 1M input at $1.50, 1M output at $7.50, 1M cache reads at a tenth of input,
        // 1M cache writes at a quarter over it.
        var usage = new LanguageModelUsage(1_000_000, 1_000_000, 1_000_000, 1_000_000);

        Assert.Equal(1.5m + 7.5m + 1.5m * 1.25m + 1.5m * 0.1m,
            TranslationBudget.Cost(usage, 1.5m, 7.5m));
    }

    [Fact]
    public void CostOfNothingIsNothing()
    {
        Assert.Equal(0m, TranslationBudget.Cost(new LanguageModelUsage(0, 0), 1.5m, 7.5m));
    }
}
