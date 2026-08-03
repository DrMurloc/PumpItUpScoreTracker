using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Takes one deep scan from an account's monthly balance. False means they had none left.
///     Identity owns the User row, so the Official Mirror asks for a scan rather than reaching
///     for the column itself (ADR-001: writes are owned by their vertical).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SpendDeepScanCommand(Guid UserId) : IRequest<bool>;
