namespace ScoreTracker.ExplorationTests.Catalog;

/// <summary>
///     Marks a test that reads a real, populated ScoreTracker database — the owner's prod-synced
///     local Aspire SQL, or a read replica. Manual runs only, never the PR gate: CI's database is
///     an empty migrated schema, which would fail every catalog assertion for the wrong reason.
///     Configure <c>CatalogProbe:ConnectionString</c> in the shared user-secrets store (the Aspire
///     AppHost's) or the SCORETRACKER_CATALOG_CONNECTION environment variable to run.
///     <para>
///         Read-only, like the rest of this assembly: these probes SELECT and never write.
///     </para>
/// </summary>
public sealed class CatalogProbeFactAttribute : FactAttribute
{
    public CatalogProbeFactAttribute()
    {
        if (!CatalogProbeConfiguration.ConnectionConfigured)
            Skip = "Catalog probe: configure the CatalogProbe:ConnectionString user-secret (AppHost " +
                   "store) or the SCORETRACKER_CATALOG_CONNECTION env var to run against a populated database.";
    }
}
