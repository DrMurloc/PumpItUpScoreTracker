namespace ScoreTracker.Web.Configuration;

/// <summary>
///     Dev-harness prod sync source, flowed from the Aspire AppHost. The API token is
///     pasted on the /Dev/Populate page and kept in the local database, not config.
/// </summary>
public sealed class ProdSyncConfiguration
{
    public string BaseUrl { get; set; } = "https://piuscores.arroweclip.se";
}
