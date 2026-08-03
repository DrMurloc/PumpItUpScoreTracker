namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Rebuilds a local development database from the live site's public API.
///     <para>
///         One method, primitives only. The predecessor put a snapshot record and six row types in
///         Domain — eight public types in the layer every other layer depends on, for a tool that
///         only ever runs on a developer's laptop. Everything about how the catalog is fetched,
///         mapped and written is now internal to <c>ScoreTracker.Data/DevTooling/</c>; this exists
///         solely because Web must not touch EF directly.
///     </para>
///     <para>
///         It reads <c>api/v2/*</c> with a personal token, exactly as an integrator would. That is
///         the point rather than a convenience: if the harness can rebuild a working database from
///         the published surface then the published surface is complete.
///     </para>
/// </summary>
public interface IDevEnvironmentSeeder
{
    Task PopulateFromApi(string apiToken, Guid localUserId, Action<string> reportProgress,
        CancellationToken cancellationToken = default);
}
