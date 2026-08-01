using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Wiring;

/// <summary>
///     Registers the Community Tools entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4).
///     <para>
///         Deliberately empty in the commit that introduces the vertical. The registration in
///         <c>VerticalModelContributions.All()</c> is the thing worth proving first: a vertical
///         missing from that list has its tables silently dropped from every scaffolded migration,
///         and the failure surfaces as missing data rather than as a build error.
///     </para>
/// </summary>
public sealed class CommunityToolsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
    }
}
