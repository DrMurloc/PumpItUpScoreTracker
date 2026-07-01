using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DeleteRandomSettingsCommand(Name SettingsName) : IRequest
    {
    }
}
