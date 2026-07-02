using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record SaveRandomSettingsCommand
    (Guid TournamentId, Name SettingsName, RandomSettings Settings) : IRequest
{
}
