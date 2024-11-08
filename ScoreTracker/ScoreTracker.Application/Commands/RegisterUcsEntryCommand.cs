using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record RegisterUcsEntryCommand(Guid ChartId, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken,
        Uri? VideoPath, Uri? ImagePath) : IRequest
    {
    }
}
