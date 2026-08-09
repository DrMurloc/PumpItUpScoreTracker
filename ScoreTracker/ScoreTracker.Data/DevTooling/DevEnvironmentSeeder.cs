using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.DevTooling;

/// <summary>
///     The one public type in this folder: everything else about rebuilding a dev database — the
///     API reads, the wire shapes, the mapping, the SQL — is internal to it.
///     <para>
///         Assembling the two halves here rather than in Web is what keeps the seeding logic in one
///         place. Web gets a port with a primitive signature and a Razor page; it does not learn what
///         a catalog snapshot is, and Domain does not carry six row types for a laptop tool.
///     </para>
/// </summary>
public sealed class DevEnvironmentSeeder : IDevEnvironmentSeeder
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IOptions<ProdSyncConfiguration> _options;

    public DevEnvironmentSeeder(IDbContextFactory<ChartAttemptDbContext> factory,
        IOptions<ProdSyncConfiguration> options, IMemoryCache cache)
    {
        _factory = factory;
        _options = options;
        _cache = cache;
    }

    public Task PopulateFromApi(string apiToken, Guid localUserId, Action<string> reportProgress,
        CancellationToken cancellationToken = default)
    {
        var reader = new DevApiReader(new DevCatalogWriter(_factory),
            _options.Value.BaseUrl, _cache);
        return reader.Populate(apiToken, localUserId, reportProgress, cancellationToken);
    }
}

/// <summary>Where the harness pulls from. Moved out of Web with the rest of the seeding.</summary>
[ExcludeFromCodeCoverage]
public sealed class ProdSyncConfiguration
{
    public const string SectionName = "ProdSync";

    public string BaseUrl { get; set; } = "https://piuscores.arroweclip.se/";
}
