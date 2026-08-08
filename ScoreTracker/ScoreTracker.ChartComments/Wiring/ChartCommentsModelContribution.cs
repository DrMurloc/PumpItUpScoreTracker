using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Wiring;

/// <summary>
///     Registers the chart-comment entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Listed in <c>VerticalModelContributions.All()</c>, without which scaffolded
///     migrations silently drop every table below.
/// </summary>
public sealed class ChartCommentsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommentEntity>().ToTable("ChartComment");
        modelBuilder.Entity<CommentRevisionEntity>().ToTable("ChartCommentRevision");
        modelBuilder.Entity<CommentVoteEntity>().ToTable("ChartCommentVote");
        modelBuilder.Entity<CommentConsentEntity>().ToTable("ChartCommentConsent");
    }
}
