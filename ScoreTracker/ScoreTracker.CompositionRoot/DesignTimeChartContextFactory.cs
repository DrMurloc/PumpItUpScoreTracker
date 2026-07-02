using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CompositionRoot;

// Lets `dotnet ef migrations add` run without booting the Web host (which requires
// real OAuth/Discord/SQL configuration at startup). Scaffolding never connects, so
// the connection string is a placeholder. Lives in CompositionRoot (not Data) because
// the design-time model must include every vertical's IDbModelContribution — run
// scaffolding with `--startup-project ../ScoreTracker.CompositionRoot` from Data.
internal sealed class DesignTimeChartContextFactory : IDesignTimeDbContextFactory<ChartAttemptDbContext>
{
    public ChartAttemptDbContext CreateDbContext(string[] args)
    {
        return new ChartAttemptDbContext(new DbContextOptionsBuilder<ChartAttemptDbContext>()
                .UseSqlServer(
                    "Server=localhost;Database=DesignTimeOnly;Integrated Security=true;TrustServerCertificate=true",
                    // Migration bundles are built from this factory and keep these options
                    // (only the connection string is swapped in at deploy time). Data-moving
                    // migrations (e.g. the ScoreEventJournal backfill) can run for minutes
                    // against prod-sized tables — SqlClient's default 30s killed the
                    // 2026-07-02 deploy.
                    o => o.CommandTimeout(3600))
                .Options,
            VerticalModelContributions.All());
    }
}
