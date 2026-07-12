using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Randomizer.Infrastructure.Entities;

namespace ScoreTracker.Randomizer.Wiring;

/// <summary>
///     Registers the Randomizer's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). UserRandomSettings moved here from the Catalog contribution with the
///     vertical extraction — table mapping unchanged, CLR namespace only.
/// </summary>
public sealed class RandomizerModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRandomSettingsEntity>().ToTable("UserRandomSettings");
        modelBuilder.Entity<UserRandomSettingsEntity>()
            .Property(e => e.Mix)
            .HasDefaultValue("Phoenix");
        modelBuilder.Entity<UserRandomSettingsEntity>()
            .HasIndex(e => e.ShareToken)
            .IsUnique()
            .HasFilter("[ShareToken] IS NOT NULL");

        modelBuilder.Entity<RandomizerDrawEntity>().ToTable("RandomizerDraw");
        modelBuilder.Entity<RandomizerDrawEntity>().HasIndex(e => e.Slug).IsUnique();
        // Personal keeps one rolling draw (the filtered unique index is the invariant);
        // tournaments hold many named draws (matches), so theirs is a plain index.
        modelBuilder.Entity<RandomizerDrawEntity>()
            .HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");
        modelBuilder.Entity<RandomizerDrawEntity>()
            .HasIndex(e => e.TournamentId);

        modelBuilder.Entity<RandomizerDrawCardEntity>().ToTable("RandomizerDrawCard")
            .HasOne<RandomizerDrawEntity>()
            .WithMany()
            .HasForeignKey(e => e.DrawId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TournamentRandomSettingsEntity>().ToTable("TournamentRandomSettings");
    }
}
