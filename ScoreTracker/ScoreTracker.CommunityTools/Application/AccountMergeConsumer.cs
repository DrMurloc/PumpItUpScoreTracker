using MassTransit;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Events;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Two accounts merged: every tool the retired account made moves to the account kept, so a
///     maker keeps all of their tools whichever account they keep. The retired account's other data
///     is purged after the grace window, and its tools would go with it. The maker is connected to
///     each tool that moves, as they were when they registered it.
///     <para>
///         A banned maker's tools stay behind and leave with the retired account: moving them would
///         lift the ban from tools it switched off.
///     </para>
/// </summary>
internal sealed class AccountMergeConsumer : IConsumer<AccountsMergedEvent>
{
    private readonly IToolMakerBanRepository _bans;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IToolRepository _tools;

    public AccountMergeConsumer(IToolRepository tools, IToolMakerBanRepository bans, IDateTimeOffsetAccessor dateTime)
    {
        _tools = tools;
        _bans = bans;
        _dateTime = dateTime;
    }

    public async Task Consume(ConsumeContext<AccountsMergedEvent> context)
    {
        var merged = context.Message;
        if (await _bans.GetBan(merged.RetiredUserId, context.CancellationToken) is not null) return;

        foreach (var toolId in await _tools.MoveTools(merged.RetiredUserId, merged.SurvivorUserId,
                     context.CancellationToken))
            await _tools.GrantShare(toolId, merged.SurvivorUserId, ShareSource.Direct, _dateTime.Now,
                context.CancellationToken);
    }
}
