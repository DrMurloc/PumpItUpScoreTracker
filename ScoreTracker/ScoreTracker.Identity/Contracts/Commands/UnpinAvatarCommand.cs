using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Back to following the official profile. The avatar the last import saw is restored
///     immediately rather than at the next import, which is why it was kept all along.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record UnpinAvatarCommand : IRequest;
