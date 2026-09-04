using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     The player chooses an avatar. From here on imports leave it alone, though they keep
///     recording what they saw so <see cref="UnpinAvatarCommand" /> can put it back at once.
/// </summary>
/// <param name="ImageUrl">
///     A picture url from the avatar catalog. Anything outside the piuimages avatar prefix is
///     rejected — a profile picture must not be an arbitrary address on the internet.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PinAvatarCommand(Uri ImageUrl) : IRequest;
