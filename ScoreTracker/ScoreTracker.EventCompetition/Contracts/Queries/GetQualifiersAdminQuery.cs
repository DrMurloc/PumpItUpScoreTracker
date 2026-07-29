using MediatR;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    /// <summary>
    ///     The organiser's view, including photos. The handler re-checks the caller's tournament
    ///     role — a hidden button is not an authorization boundary.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetQualifiersAdminQuery(Guid TournamentId) : IQuery<QualifierAdminView>
    {
    }
}
