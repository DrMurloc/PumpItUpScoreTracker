using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveUserRandomSettingsCommand(Name SettingsName, RandomSettings Settings,
        MixEnum Mix = MixEnum.Phoenix) : IRequest
    {
    }
}
