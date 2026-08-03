using ScoreTracker.CommunityTools.Contracts;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     Asks whether a maker's source repository is actually readable by the players who are being
///     told to go and read it.
///     <para>
///         Vertical-local rather than a shared Domain port, for the same reason
///         <c>IWebhookDeliveryClient</c> is: a vertical that talks to a remote owns its client.
///     </para>
/// </summary>
internal interface IRepositoryReachabilityClient
{
    Task<RepositoryReachability> Check(Uri repositoryUrl, CancellationToken cancellationToken = default);
}

/// <summary>
///     What one anonymous fetch produced, in the console's closed vocabulary. No exception text —
///     the same rule the webhook console follows, and for the same reason.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record RepositoryReachability(bool Reachable, WebhookFailureReason Reason, int? StatusCode)
{
    public static RepositoryReachability Ok(int statusCode)
    {
        return new RepositoryReachability(true, WebhookFailureReason.None, statusCode);
    }

    public static RepositoryReachability Failed(WebhookFailureReason reason, int? statusCode)
    {
        return new RepositoryReachability(false, reason, statusCode);
    }
}
