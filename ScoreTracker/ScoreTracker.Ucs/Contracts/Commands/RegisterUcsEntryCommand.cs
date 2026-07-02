using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Ucs.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RegisterUcsEntryCommand(Guid ChartId, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken,
    Uri? VideoPath, Uri? ImagePath) : IRequest
{
}
