using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Sets the current user's game tag and avatar from official account data (sent by
///     the OfficialMirror import saga after scraping account info). All other user fields
///     are preserved.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UpdateUserGameProfileCommand(Name GameTag, Uri AvatarUrl) : IRequest
{
}
