using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api
{
    [ApiToken]
    [Route("api/tournaments")]
    [DisableCors]
    public class TournamentController : Controller
    {
        [HttpGet]
        public async Task<IActionResult> GetTournaments([FromServices] IMediator mediator)
        {
            return Ok((await mediator.Send(new GetAllTournamentsQuery(), HttpContext.RequestAborted))
                .Select(t => new TournamentDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Type = t.Type.ToString()
                }));
        }

        [HttpGet("{tournamentId:guid}/matches")]
        public async Task<IActionResult> GetMatches([FromServices] IMediator mediator, [FromRoute] Guid tournamentId,
            [FromQuery] string[] name, [FromQuery] string[] phase, [FromQuery] string[] state)
        {
            var names = name.Where(s => Name.TryParse(s, out _)).Select(Name.From).Distinct().ToHashSet();
            var phases = phase.Where(s => Name.TryParse(s, out _)).Select(Name.From).Distinct().ToHashSet();
            var statuses = state.Where(s => Enum.TryParse<MatchState>(s, out _))
                .Select(Enum.Parse<MatchState>).Distinct().ToHashSet();
            var matches = await mediator.Send(new GetAllMatchesQuery(tournamentId), HttpContext.RequestAborted);

            var matchLinks = (await mediator.Send(new GetMatchLinksQuery(tournamentId))).GroupBy(m => m.FromMatch)
                .ToDictionary(kv => kv.Key, kv => kv.ToArray());
            if (statuses.Any()) matches = matches.Where(m => statuses.Contains(m.State));

            if (names.Any()) matches = matches.Where(m => names.Contains(m.MatchName));

            if (phases.Any()) matches = matches.Where(m => phases.Contains(m.PhaseName));

            var charts = (await mediator.Send(new GetChartsQuery(MixEnum.Phoenix))).ToDictionary(c => c.Id);
            var result = matches.Select(m => new MatchDto
            {
                Charts = m.ActiveCharts.Select(c => charts[c].Song.Name + " " + charts[c].DifficultyString).ToArray(),
                VetoedCharts = m.VetoedCharts.Select(c => charts[c].Song.Name + " " + charts[c].DifficultyString)
                    .ToArray(),
                FinalPlaces = m.FinalPlaces.Select(p => p.ToString()).ToArray(),
                Name = m.MatchName,
                Winners =
                    m.State != MatchState.Completed ? Array.Empty<string>() :
                    matchLinks.TryGetValue(m.MatchName, out var links) ? links.Where(l => l.IsWinners)
                        .SelectMany(l => Enumerable.Range(0, l.PlayerCount).Select(i => m.FinalPlaces[i]))
                        .Distinct().Select(p => p.ToString()).ToArray() :
                    m.FinalPlaces.Length > 0 ? new[] { m.FinalPlaces[0].ToString() } : Array.Empty<string>(),
                Phase = m.PhaseName,
                Players = m.Players.Select(p => p.ToString()).ToArray(),
                Scores = m.Scores.ToDictionary(kv => kv.Key,
                    kv => (IDictionary<string, int?>)kv.Value.Select((s, i) => (s, i)).ToDictionary(
                        t => charts[m.ActiveCharts[t.i]].Song.Name + " " + charts[m.ActiveCharts[t.i]].DifficultyString,
                        t => t.s == 0 ? null : (int?)t.s)),
                Points = m.Points.ToDictionary(kv => kv.Key,
                    kv => (IDictionary<string, int>)kv.Value.Select((s, i) => (s, i)).ToDictionary(
                        t => charts[m.ActiveCharts[t.i]].Song.Name + " " + charts[m.ActiveCharts[t.i]].DifficultyString,
                        t => t.s)),
                State = m.State.ToString()
            });
            return Ok(result);
        }
    }
}
