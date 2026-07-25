using Microsoft.Extensions.Configuration;

namespace ScoreTracker.ExplorationTests.Catalog;

/// <summary>
///     Where the catalog probes get their database. Shares the Aspire AppHost's user-secrets store
///     (see the csproj), so the local Aspire SQL connection string can be configured once:
///     <c>dotnet user-secrets set "CatalogProbe:ConnectionString" "..." --project ScoreTracker/ScoreTracker.AppHost</c>.
///     The environment variable wins when both are set.
/// </summary>
internal static class CatalogProbeConfiguration
{
    private static readonly Lazy<IConfigurationRoot> Configuration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets<CatalogProbeFactAttribute>(true)
            .Build());

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("SCORETRACKER_CATALOG_CONNECTION")
        ?? Configuration.Value["CatalogProbe:ConnectionString"];

    public static bool ConnectionConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
