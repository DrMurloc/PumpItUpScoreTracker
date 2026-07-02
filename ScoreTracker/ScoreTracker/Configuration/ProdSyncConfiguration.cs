namespace ScoreTracker.Web.Configuration;

/// <summary>
///     Dev-harness prod sync settings, flowed from the Aspire AppHost (token from AppHost
///     user-secrets, base URL from AppHost appsettings). Empty token = sync unavailable.
/// </summary>
public sealed class ProdSyncConfiguration
{
    public string ApiToken { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://piuscores.arroweclip.se";
}
