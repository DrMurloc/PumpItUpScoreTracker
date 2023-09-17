using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Commands
{
    public sealed record SaveTournamentCommand(TournamentConfiguration Tournament) : IRequest

    {
    }
}