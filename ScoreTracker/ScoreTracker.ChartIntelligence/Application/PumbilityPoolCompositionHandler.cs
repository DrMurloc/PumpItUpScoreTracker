using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;

namespace ScoreTracker.ChartIntelligence.Application;

internal sealed class PumbilityPoolCompositionHandler
    : IRequestHandler<GetPumbilityPoolCompositionQuery, PumbilityPoolCompositionRecord?>
{
    private readonly IMemoryCache _cache;
    private readonly IPumbilityPoolCompositionRepository _composition;

    public PumbilityPoolCompositionHandler(IPumbilityPoolCompositionRepository composition, IMemoryCache cache)
    {
        _composition = composition;
        _cache = cache;
    }

    public async Task<PumbilityPoolCompositionRecord?> Handle(GetPumbilityPoolCompositionQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{nameof(PumbilityPoolCompositionHandler)}_{request.Mix}";
        if (_cache.TryGetValue<PumbilityPoolCompositionRecord?>(cacheKey, out var cached) && cached != null)
            return cached;

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            var record = await _composition.Get(request.Mix, cancellationToken);
            // The rows change once a night, so an hour is generous. A missing mix is cached for a
            // minute only: "not built yet" almost always means the owner is about to press Rebuild,
            // and an hour would turn the press into "press and wait until tomorrow".
            entry.AbsoluteExpirationRelativeToNow = record == null ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(1);
            return record;
        });
    }
}
