using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Removes every external login the current account holds for one provider — a provider is
///     one sign-in method even when it spans multiple alias rows (PiuGame). Disconnecting the
///     account's only remaining provider throws so an account can't lock itself out.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DisconnectExternalProviderCommand(string LoginProviderName) : IRequest
{
}
