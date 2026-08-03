using System;
using System.Linq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The Tool aggregate's invariants: the listing flow, and the gate on entering PIUGame-session
///     mode. Both live in the aggregate rather than in a handler because a handler is one caller.
/// </summary>
public sealed class ToolTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri Hook = new("https://pumbility.app/hooks/piuscores");

    private static Tool NewTool()
    {
        return Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From("Pumbility Planner"), Now);
    }

    /// <summary>
    ///     A tool that has cleared everything listing needs: a description, a source players can
    ///     read, and a maker who can be reached.
    /// </summary>
    private static Tool ListedTool()
    {
        var tool = ShareableTool();
        tool.Describe(Name.From("Pumbility Planner"), "Plans what to push next.", null, Repository);
        tool.MarkRepositoryReachable(Now);
        tool.RequestListing();
        tool.Approve(Now);
        return tool;
    }

    [Fact]
    public void ANewToolStartsPrivateAndFullyFunctional()
    {
        var tool = NewTool();

        Assert.Equal(ToolVisibility.Private, tool.Visibility);
        Assert.Equal(WebhookMode.None, tool.WebhookMode);
        Assert.True(tool.AcceptsAllToolsShare);
    }

    // Players read the description before deciding to connect; listing without one asks them to
    // trust a name alone.
    [Fact]
    public void ListingWithoutADescriptionIsRefused()
    {
        var tool = NewTool();

        Assert.Throws<ToolListingException>(() => tool.RequestListing());
    }

    [Fact]
    public void ApprovalMovesAToolIntoTheDirectoryAndClearsAnyPriorRejection()
    {
        var tool = ShareableTool();
        tool.Describe(Name.From("Planner"), "Plans things.", null, Repository);
        tool.MarkRepositoryReachable(Now);
        tool.RequestListing();
        tool.Reject("Needs a link.");
        tool.RequestListing();
        tool.Approve(Now);

        Assert.Equal(ToolVisibility.Public, tool.Visibility);
        Assert.Equal(Now, tool.ApprovedAt);
        Assert.Null(tool.RejectionReason);
    }

    [Fact]
    public void RejectionNeedsAReasonTheMakerCanActOn()
    {
        var tool = ShareableTool();
        tool.Describe(Name.From("Planner"), "Plans things.", null, Repository);
        tool.MarkRepositoryReachable(Now);
        tool.RequestListing();

        Assert.Throws<ToolListingException>(() => tool.Reject("  "));
    }

    [Fact]
    public void OnlyAToolAwaitingReviewCanBeApprovedOrRejected()
    {
        Assert.Throws<ToolListingException>(() => NewTool().Approve(Now));
        Assert.Throws<ToolListingException>(() => NewTool().Reject("no"));
    }

    // A maker could otherwise pass review as one thing and rename to another the next day.
    [Fact]
    public void RenamingAListedToolSendsItBackForReview()
    {
        var tool = ListedTool();

        tool.Describe(Name.From("Something Else"), "Plans what to push next.", null, null);

        Assert.Equal(ToolVisibility.PendingApproval, tool.Visibility);
        Assert.Null(tool.ApprovedAt);
    }

    [Fact]
    public void RewritingTheDescriptionAlsoSendsItBackForReview()
    {
        var tool = ListedTool();

        tool.Describe(Name.From("Pumbility Planner"), "Actually it does something else now.", null, null);

        Assert.Equal(ToolVisibility.PendingApproval, tool.Visibility);
    }

    [Fact]
    public void SavingAListedToolWithoutChangingWhatPlayersSeeKeepsItListed()
    {
        var tool = ListedTool();

        tool.Describe(Name.From("Pumbility Planner"), "Plans what to push next.", null, Repository);

        Assert.Equal(ToolVisibility.Public, tool.Visibility);
    }

    [Theory]
    [InlineData(WebhookMode.PlayerPing)]
    [InlineData(WebhookMode.ScorePush)]
    [InlineData(WebhookMode.None)]
    public void MovingWithinTheReadTierIsFreeEvenWithPlayersConnected(WebhookMode mode)
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);

        tool.SetWebhook(mode, Hook, 1204, hasOutboundHeader: true);

        Assert.Equal(mode, tool.WebhookMode);
    }

    // The players already connected agreed to score reads, not to handing over a piugame session.
    [Fact]
    public void EnteringSessionModeWithPlayersConnectedIsRefused()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);

        var error = Assert.Throws<ToolWebhookModeException>(() =>
            tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 1204, hasOutboundHeader: true));

        Assert.Contains("1204", error.Message);
        Assert.Equal(WebhookMode.ScorePush, tool.WebhookMode);
    }

    [Fact]
    public void EnteringSessionModeWithNobodyConnectedIsAllowed()
    {
        var tool = NewTool();

        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: true);

        Assert.Equal(WebhookMode.PiuGameSession, tool.WebhookMode);
        Assert.True(tool.RequiresExplicitShare);
    }

    // Staying in session mode is not entering it; a maker who is already there can still edit
    // the URL without disconnecting their players first.
    [Fact]
    public void StayingInSessionModeIsNotGatedOnPlayerCount()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: true);

        tool.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://elsewhere.example/hook"), 1204, hasOutboundHeader: true);

        Assert.Equal("https://elsewhere.example/hook", tool.WebhookUrl!.ToString());
    }

    [Fact]
    public void LeavingSessionModeIsAlwaysAllowed()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: true);

        tool.SetWebhook(WebhookMode.ScorePush, Hook, 1204, hasOutboundHeader: true);

        Assert.Equal(WebhookMode.ScorePush, tool.WebhookMode);
        Assert.False(tool.RequiresExplicitShare);
    }

    [Fact]
    public void ADeliveryModeNeedsSomewhereToDeliver()
    {
        Assert.Throws<ToolWebhookModeException>(() => NewTool().SetWebhook(WebhookMode.ScorePush, null, 0, hasOutboundHeader: true));
    }

    [Fact]
    public void TurningDeliveryOffClearsTheUrl()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);

        tool.SetWebhook(WebhookMode.None, Hook, 0, hasOutboundHeader: true);

        Assert.Null(tool.WebhookUrl);
    }

    [Fact]
    public void MixSubscriptionsReplaceRatherThanAccumulate()
    {
        var tool = NewTool();
        tool.SetMixes(new[] { MixEnum.Phoenix, MixEnum.Phoenix2 });

        tool.SetMixes(new[] { MixEnum.FiestaEx });

        Assert.Equal(new[] { MixEnum.FiestaEx }, tool.Mixes.ToArray());
    }

    // A configured URL is a claim. Until the endpoint echoes our challenge it is not a destination,
    // and every delivery path asks CanDeliver rather than asking whether a URL is set.
    [Fact]
    public void AConfiguredButUnverifiedUrlDeliversNothing()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);

        Assert.False(tool.CanDeliver);

        tool.MarkWebhookVerified(Now);

        Assert.True(tool.CanDeliver);
    }

    // Verify once and swap to anything would make the whole handshake decorative.
    [Fact]
    public void ChangingTheUrlClearsVerification()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);
        tool.MarkWebhookVerified(Now);

        tool.SetWebhook(WebhookMode.ScorePush, new Uri("https://elsewhere.example/hook"), 0,
            hasOutboundHeader: true);

        Assert.Null(tool.WebhookUrlVerifiedAt);
        Assert.False(tool.CanDeliver);
    }

    // Changing only the mode is not changing the destination, so re-proving the same URL would be
    // ceremony — and ceremony is what makes people paste a URL they have not read.
    [Fact]
    public void ChangingModeWithTheSameUrlKeepsVerification()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);
        tool.MarkWebhookVerified(Now);

        tool.SetWebhook(WebhookMode.PlayerPing, Hook, 0, hasOutboundHeader: true);

        Assert.Equal(Now, tool.WebhookUrlVerifiedAt);
    }

    // Turning delivery off drops the URL, so there is nothing left that was proven.
    [Fact]
    public void TurningDeliveryOffClearsVerification()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.ScorePush, Hook, 0, hasOutboundHeader: true);
        tool.MarkWebhookVerified(Now);

        tool.SetWebhook(WebhookMode.None, Hook, 0, hasOutboundHeader: true);

        Assert.Null(tool.WebhookUrlVerifiedAt);
        Assert.False(tool.CanDeliver);
    }

    // The mode that hands over a live piugame key needs the maker's endpoint to be able to tell our
    // call from anyone else's. The other modes do not — that risk is the maker's own system.
    [Fact]
    public void SessionModeNeedsAnOutboundHeader()
    {
        var tool = NewTool();

        Assert.Throws<ToolWebhookModeException>(() =>
            tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: false));

        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: true);
        Assert.Equal(WebhookMode.PiuGameSession, tool.WebhookMode);
    }

    [Theory]
    [InlineData(WebhookMode.ScorePush)]
    [InlineData(WebhookMode.PlayerPing)]
    public void TheReadModesDoNotNeedAnOutboundHeader(WebhookMode mode)
    {
        var tool = NewTool();

        tool.SetWebhook(mode, Hook, 0, hasOutboundHeader: false);

        Assert.Equal(mode, tool.WebhookMode);
    }

    [Fact]
    public void ThereIsNothingToVerifyWithoutAUrl()
    {
        Assert.Throws<ToolWebhookModeException>(() => NewTool().MarkWebhookVerified(Now));
    }

    // Entry-only, like the zero-players rule. PIU Tracker was seeded straight into session mode by
    // migration and has no header; without this its own maker cannot edit its description.
    [Fact]
    public void AToolAlreadyInSessionModeCanStillBeEditedWithoutAHeader()
    {
        var tool = NewTool();
        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: true);

        tool.SetWebhook(WebhookMode.PiuGameSession, Hook, 0, hasOutboundHeader: false);

        Assert.Equal(WebhookMode.PiuGameSession, tool.WebhookMode);
    }

    private static readonly Uri Repository = new("https://github.com/errlena/pumbility-planner");

    /// <summary>The three things that must be true before another player's scores are involved.</summary>
    private static Tool ShareableTool()
    {
        var tool = Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From("Pumbility Planner"), Now,
            Repository, "errlena", Now);
        tool.MarkRepositoryReachable(Now);
        return tool;
    }

    [Fact]
    public void ANewToolCannotBeSharedWithAnyoneButItsMaker()
    {
        Assert.False(NewTool().CanBeSharedWithOthers);
    }

    [Fact]
    public void ARepositoryAndAHandleAndACheckTogetherOpenTheGate()
    {
        Assert.True(ShareableTool().CanBeSharedWithOthers);
    }

    [Fact]
    public void AnUncheckedRepositoryIsNotEnough()
    {
        var tool = Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From("Planner"), Now,
            Repository, "errlena", Now);

        Assert.False(tool.CanBeSharedWithOthers);
    }

    [Fact]
    public void ARepositoryWithoutAHandleIsNotEnough()
    {
        var tool = Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From("Planner"), Now,
            Repository, null, Now);
        tool.MarkRepositoryReachable(Now);

        Assert.False(tool.CanBeSharedWithOthers);
    }

    [Fact]
    public void ABlankHandleDoesNotCountAsAHandle()
    {
        var tool = ShareableTool();
        tool.SetDiscordHandle("   ");

        Assert.False(tool.CanBeSharedWithOthers);
    }

    // Otherwise: check once, swap to anything. Same rule as the webhook proof, same reason.
    [Fact]
    public void ChangingTheRepositoryWithdrawsItsCheck()
    {
        var tool = ShareableTool();

        tool.Describe(tool.Name, tool.Description, tool.Url,
            new Uri("https://github.com/someone-else/a-different-thing"));

        Assert.Null(tool.RepositoryCheckedAt);
        Assert.False(tool.CanBeSharedWithOthers);
    }

    [Fact]
    public void SavingTheSameRepositoryAgainKeepsItsCheck()
    {
        var tool = ShareableTool();

        tool.Describe(tool.Name, tool.Description, tool.Url, Repository);

        Assert.Equal(Now, tool.RepositoryCheckedAt);
    }

    // The repository is printed beside the tool in the directory, so swapping it after approval is
    // renaming wearing a different hat.
    [Fact]
    public void ChangingTheRepositoryOfAListedToolReturnsItToReview()
    {
        var tool = ListedTool();

        tool.Describe(tool.Name, tool.Description, tool.Url,
            new Uri("https://github.com/someone-else/a-different-thing"));

        Assert.Equal(ToolVisibility.PendingApproval, tool.Visibility);
        Assert.Null(tool.ApprovedAt);
    }

    [Theory]
    [InlineData("https://github.com/errlena/planner", "errlena")]
    [InlineData("https://gitlab.com/errlena/planner", "errlena")]
    [InlineData("https://codeberg.org/errlena/planner/", "errlena")]
    [InlineData("https://git.example.test/errlena", "errlena")]
    public void TheRepositoryOwnerIsTheFirstPathSegment(string url, string expected)
    {
        var tool = Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From("Planner"), Now,
            new Uri(url), "errlena", Now);

        Assert.Equal(expected, tool.RepositoryOwner);
    }

    [Fact]
    public void ARepositoryHostWithNoPathHasNoOwner()
    {
        var tool = Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From("Planner"), Now,
            new Uri("https://git.example.test/"), "errlena", Now);

        Assert.Null(tool.RepositoryOwner);
    }

    // PIU Tracker arrived Public with 653 migrated players before the rule existed. Gating it would
    // take a working integration away from them to enforce something written afterwards.
    [Fact]
    public void TheGrandfatheredToolIsShareableWithNothingSet()
    {
        var tool = Tool.Create(GrandfatheredTools.PiuTracker, Guid.NewGuid(),
            Name.From("PIU Tracker"), Now);

        Assert.True(tool.CanBeSharedWithOthers);
    }

    [Fact]
    public void AListedToolStillNeedsADescription()
    {
        var tool = ShareableTool();

        Assert.Throws<ToolListingException>(() => tool.RequestListing());
    }

    // Being listed is an invitation to every player on the site, so the source they are invited to
    // read has to be readable and someone has to be reachable when it goes wrong.
    [Fact]
    public void AToolWithNoCheckedSourceCannotAskToBeListed()
    {
        var tool = NewTool();
        tool.Describe(Name.From("Planner"), "Plans what to push next.", null, Repository);

        Assert.Throws<ToolRepositoryRequiredException>(() => tool.RequestListing());
    }
}
