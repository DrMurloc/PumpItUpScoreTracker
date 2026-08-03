using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     Served from <see cref="MixEnum" /> rather than the Mix table. The table seeds sort order and
///     the primary flag, but the enum's helpers are what every page, picker and formula in the app
///     actually reads — so answering from the enum reports what the app uses and cannot drift from it.
/// </summary>
internal sealed class GetMixesHandler : IRequestHandler<GetMixesQuery, IReadOnlyList<MixRecord>>
{
    public Task<IReadOnlyList<MixRecord>> Handle(GetMixesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<MixRecord> mixes = Enum.GetValues<MixEnum>()
            .OrderBy(m => m.DisplayOrder())
            .Select(m => new MixRecord(m, m.ToString(), m.GetName(), m.DisplayOrder(), m.IsPrimary(),
                m.UsesLegacyScoring()))
            .ToArray();
        return Task.FromResult(mixes);
    }
}
