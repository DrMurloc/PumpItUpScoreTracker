using ScoreTracker.Communities.Contracts;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>Everything /Community/Discord draws, for the viewer asking.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityDiscordQuery(Name CommunityName) : IQuery<CommunityDiscordView>;

/// <summary>
///     The roles the server has, for the picker. Empty when no server is designated or the bot
///     cannot see it — each role already carries whether the bot may hand it out.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityDiscordRolesQuery(Name CommunityName)
    : IQuery<IReadOnlyList<BotGuildRole>>;

/// <summary>
///     What the next pass would change, without changing it. Computed by the same planner the
///     real reconcile uses, so the two cannot disagree.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityDiscordPreviewQuery(Name CommunityName)
    : IQuery<IReadOnlyList<DiscordRoleChangeRecord>>;
