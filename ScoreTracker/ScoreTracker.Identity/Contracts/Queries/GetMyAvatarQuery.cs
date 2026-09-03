using ScoreTracker.SharedKernel.Messaging;

using ScoreTracker.Identity.Contracts;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     What the current player's avatar is, and whether they chose it. The Account page needs the
///     distinction; nothing that merely renders an avatar does.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyAvatarQuery : IQuery<MyAvatarRecord>;
