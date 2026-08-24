using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Translations.Infrastructure.Entities;

namespace ScoreTracker.Translations.Wiring;

/// <summary>
///     Registers the pipeline's tables with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Listed in <c>VerticalModelContributions.All()</c>, without which scaffolded
///     migrations silently drop both tables below.
/// </summary>
public sealed class TranslationsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranslationRequestEntity>().ToTable("TranslationRequest");
        modelBuilder.Entity<TranslationBatchEntity>().ToTable("TranslationBatch");
    }
}
