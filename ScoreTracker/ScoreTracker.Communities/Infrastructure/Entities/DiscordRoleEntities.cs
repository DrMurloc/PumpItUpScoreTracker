using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Communities.Infrastructure.Entities;

/// <summary>
///     The one Discord server a community hands out roles in. The unique index on
///     <see cref="CommunityId" /> is what makes "one server per community" a constraint rather
///     than a convention.
///     <para>
///         Deliberately NOT the same thing as a registered feed channel: a server that receives a
///         community's score cards has not thereby been claimed by it
///         (docs/design/discord-role-management.md D3).
///     </para>
/// </summary>
[Index(nameof(CommunityId), IsUnique = true)]
internal sealed class CommunityDiscordServerEntity
{
    [Key] public Guid Id { get; set; }
    public Guid CommunityId { get; set; }
    public ulong GuildId { get; set; }

    /// <summary>Denormalized for the page, refreshed whenever the bot can see the server.</summary>
    [MaxLength(128)] public string GuildName { get; set; } = string.Empty;

    public DateTimeOffset DesignatedAt { get; set; }
}

/// <summary>
///     One title mapped to one role. Unique both ways: a title cannot point at two roles, and two
///     titles cannot point at one role — the second would make "should this role come off" have no
///     single answer.
/// </summary>
[Index(nameof(CommunityId), nameof(TitleName), IsUnique = true)]
[Index(nameof(CommunityId), nameof(RoleId), IsUnique = true)]
internal sealed class CommunityTitleRoleEntity
{
    [Key] public Guid Id { get; set; }
    public Guid CommunityId { get; set; }

    /// <summary>The shipped Phoenix 2 title name, verbatim — the same string the title list keys on.</summary>
    [MaxLength(64)] public string TitleName { get; set; } = string.Empty;

    public ulong RoleId { get; set; }
}

/// <summary>
///     Who we have granted roles to, and against which Discord account. One row per
///     (community, member) — not per role, since the community's mapped roles ARE the managed set.
///     <para>
///         This table is why the feature is not stateless, and it earns its place twice. Identity's
///         purge deletes ExternalLoginEntity on its FIRST pass, immediately after an in-memory
///         publish that returns on dispatch rather than completion — so a consumer reading the
///         snowflake then is racing a delete it usually loses. And a sweep over current members
///         never visits somebody who left, so their roles would stand forever. Both readings come
///         from here instead.
///     </para>
/// </summary>
[Index(nameof(CommunityId), nameof(UserId), IsUnique = true)]
[Index(nameof(UserId))]
internal sealed class CommunityDiscordGrantEntity
{
    [Key] public Guid Id { get; set; }
    public Guid CommunityId { get; set; }

    /// <summary>The account. The purge key — this entity carries no second user column.</summary>
    public Guid UserId { get; set; }

    public ulong DiscordUserId { get; set; }
    public DateTimeOffset LastReconciledAt { get; set; }
}
