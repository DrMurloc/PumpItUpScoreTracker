using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Rivals.Infrastructure.Entities;

namespace ScoreTracker.Rivals.Wiring;

/// <summary>
///     Registers the Rivals entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Must be listed in <c>VerticalModelContributions.All()</c> or scaffolded
///     migrations silently drop every table below.
/// </summary>
public sealed class RivalsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RivalEntity>().ToTable("Rival");
        modelBuilder.Entity<RivalBlockEntity>().ToTable("RivalBlock");
        modelBuilder.Entity<RivalInviteCodeEntity>().ToTable("RivalInviteCode");

        // Two filtered uniques rather than one over both columns: exactly one target is ever
        // set, and a NULL never equals a NULL, so an unfiltered unique would let the same owner
        // store the same tag any number of times.
        modelBuilder.Entity<RivalEntity>()
            .HasIndex(e => new { e.OwnerUserId, e.TargetUserId })
            .IsUnique()
            .HasFilter("[TargetUserId] IS NOT NULL");
        modelBuilder.Entity<RivalEntity>()
            .HasIndex(e => new { e.OwnerUserId, e.TargetTag })
            .IsUnique()
            .HasFilter("[TargetTag] IS NOT NULL");

        // The reverse list ("who rivals you") seeks on the target, and the link/rename consumers
        // seek on the tag — neither is served by the owner-leading uniques above.
        modelBuilder.Entity<RivalEntity>().HasIndex(e => e.TargetUserId);
        modelBuilder.Entity<RivalEntity>().HasIndex(e => e.TargetTag);

        // The pair IS the identity; a block is stored once, from the blocker's side.
        modelBuilder.Entity<RivalBlockEntity>().HasKey(e => new { e.UserId, e.BlockedUserId });
        // Every block check reads both directions, so the reverse needs its own seek.
        modelBuilder.Entity<RivalBlockEntity>().HasIndex(e => e.BlockedUserId);

        // Redeeming a link looks the code up, not the user.
        modelBuilder.Entity<RivalInviteCodeEntity>().HasIndex(e => e.Code).IsUnique();
    }
}
