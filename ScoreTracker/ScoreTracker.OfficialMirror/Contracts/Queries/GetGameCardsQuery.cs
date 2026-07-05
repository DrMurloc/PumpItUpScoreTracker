using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetGameCardsQuery(string Username, RedactedString Password, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<GameCardRecord>>
    {
    }
}
