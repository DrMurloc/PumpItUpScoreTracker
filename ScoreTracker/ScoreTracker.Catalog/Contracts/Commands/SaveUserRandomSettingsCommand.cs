using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveUserRandomSettingsCommand(Name SettingsName, RandomSettings Settings) : IRequest
    {
    }
}
