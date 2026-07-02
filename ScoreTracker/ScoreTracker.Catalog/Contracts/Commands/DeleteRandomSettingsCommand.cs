using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DeleteRandomSettingsCommand(Name SettingsName) : IRequest
    {
    }
}
