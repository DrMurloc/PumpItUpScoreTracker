using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record DeleteRandomSettingsCommand(Name SettingsName) : IRequest
    {
    }
}
