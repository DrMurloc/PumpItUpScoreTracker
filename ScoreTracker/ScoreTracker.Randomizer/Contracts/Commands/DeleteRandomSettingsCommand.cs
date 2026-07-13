using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DeleteRandomSettingsCommand(Name SettingsName) : IRequest
    {
    }
}
