using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Identity.Infrastructure.Entities;

namespace ScoreTracker.Identity.Wiring;

/// <summary>
///     Registers the Identity vertical's own entities with the single
///     <see cref="ChartAttemptDbContext" /> (ADR-001 D4). The legacy user tables stay in
///     ScoreTracker.Data transitionally; only new Identity-owned tables live here.
/// </summary>
public sealed class IdentityModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        var merge = modelBuilder.Entity<MergeRequestEntity>();
        merge.ToTable("MergeRequest");
        merge.HasKey(m => m.Id);
        merge.Property(m => m.State).HasMaxLength(16);
        merge.HasIndex(m => m.SurvivorUserId);
        merge.HasIndex(m => m.RetiredUserId);
        merge.HasIndex(m => new { m.State, m.PurgeAfter });

        var credentialKey = modelBuilder.Entity<UserImportCredentialKeyEntity>();
        credentialKey.ToTable("UserImportCredentialKey");
        credentialKey.HasKey(k => k.KeyId);
        credentialKey.HasIndex(k => k.UserId);
    }
}
