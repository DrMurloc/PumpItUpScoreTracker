using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record SaveRandomSettingsCommand(Name SettingsName, RandomSettings Settings) : IRequest
    {
    }
}
