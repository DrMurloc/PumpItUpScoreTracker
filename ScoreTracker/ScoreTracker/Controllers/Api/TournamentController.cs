using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api
{
    [ApiToken]
    [Route("api/tournaments")]
    [EnableCors("API")]
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
    }
}
