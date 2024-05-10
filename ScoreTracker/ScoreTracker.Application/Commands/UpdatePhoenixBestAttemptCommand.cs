using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

public sealed record UpdatePhoenixBestAttemptCommand(Guid ChartId,
    bool IsBroken, PhoenixScore? Score, PhoenixPlate? Plate, bool KeepBestStats = false) : IRequest
{
}