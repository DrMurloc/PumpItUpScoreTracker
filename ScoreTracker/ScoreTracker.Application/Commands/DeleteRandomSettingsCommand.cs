using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DeleteRandomSettingsCommand(Name SettingsName) : IRequest
    {
    }
}
