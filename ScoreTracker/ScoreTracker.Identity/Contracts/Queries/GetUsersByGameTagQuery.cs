using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     Accounts carrying a game tag. Game tags are self-reported and non-unique — matches are
///     merge invitations at most, never automatic links (the merge prove step is the gate).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUsersByGameTagQuery(Name GameTag) : IQuery<IEnumerable<User>>
{
}
