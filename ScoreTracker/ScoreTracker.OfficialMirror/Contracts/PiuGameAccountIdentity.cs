using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     Everything a successful piugame credential login reveals about the account in one
///     session: the login id as typed, the active display name, the piuscores-hosted avatar,
///     and every game card (sub-profile number + tag). Consumers derive login aliases from
///     these — no single piugame identifier is known-stable, so all of them participate in
///     matching (login-overhaul design).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PiuGameAccountIdentity(
    string Username,
    Name GameTag,
    Uri ProfileImage,
    IEnumerable<GameCardRecord> Cards)
{
}
