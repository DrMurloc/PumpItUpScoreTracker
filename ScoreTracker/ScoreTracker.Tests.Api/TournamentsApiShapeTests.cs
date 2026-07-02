using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Controllers.Api;

namespace ScoreTracker.Tests.Api;

public sealed class TournamentsApiShapeTests
{
    [Fact]
    public async Task GetTournamentsPreservesResponseShape()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetAllTournamentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TournamentRecord(ApiTestData.TournamentId, Name.From("March of Murlocs 2026"), 12,
                    TournamentType.Stamina, "Online", true, null, ApiTestData.Date1, ApiTestData.Date2, true)
            });
        var controller = new TournamentController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetTournaments(mediator.Object);

        JsonApproval.AssertWireShape("""
            [
              {
                "id": "55555555-5555-5555-5555-555555555555",
                "type": "Stamina",
                "name": "March of Murlocs 2026"
              }
            ]
            """, result);
    }
}
