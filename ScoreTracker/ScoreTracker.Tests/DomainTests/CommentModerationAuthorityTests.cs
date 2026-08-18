using System;
using System.Linq;
using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CommentModerationAuthorityTests
{
    private const CommunityPermission Kit = CommunityPermission.ModerateComments;
    private const CommunityPermission NoKit = CommunityPermission.ManageUsers;

    // ----- removal: the hierarchy -------------------------------------------------------------

    [Theory]
    [InlineData(CommunityRole.Admin, true)]
    [InlineData(CommunityRole.Member, true)]
    [InlineData(CommunityRole.Banned, true)]
    [InlineData(null, true)] // author left the club; moderated like a member
    [InlineData(CommunityRole.Creator, false)] // nobody touches the creator
    public void TheCreatorModeratesEveryoneBelowThemselves(CommunityRole? authorRole, bool expected)
    {
        Assert.Equal(expected, CommentModerationAuthority.MayRemove(false, CommunityRole.Creator,
            CommunityPermission.None, authorRole));
    }

    [Theory]
    [InlineData(CommunityRole.Member, true)]
    [InlineData(CommunityRole.Banned, true)]
    [InlineData(null, true)]
    [InlineData(CommunityRole.Admin, false)] // admins never act on each other
    [InlineData(CommunityRole.Creator, false)]
    public void AnAdminWithTheFlagModeratesMembersOnly(CommunityRole? authorRole, bool expected)
    {
        Assert.Equal(expected, CommentModerationAuthority.MayRemove(false, CommunityRole.Admin, Kit,
            authorRole));
    }

    [Fact]
    public void AnAdminWithoutTheFlagModeratesNobody()
    {
        Assert.False(CommentModerationAuthority.MayRemove(false, CommunityRole.Admin, NoKit,
            CommunityRole.Member));
    }

    [Theory]
    [InlineData(CommunityRole.Member)]
    [InlineData(CommunityRole.Banned)]
    [InlineData(null)]
    public void MembersAndOutsidersModerateNobodyWhateverFlagsTheyCarry(CommunityRole? actorRole)
    {
        // Permissions are only meaningful for admins; a stray flag on a member row grants nothing.
        Assert.False(CommentModerationAuthority.MayRemove(false, actorRole, CommunityPermission.All,
            CommunityRole.Member));
    }

    [Theory]
    [InlineData(CommunityRole.Creator)]
    [InlineData(CommunityRole.Admin)]
    [InlineData(CommunityRole.Member)]
    [InlineData(null)]
    public void TheSiteAdminRemovesEverything(CommunityRole? authorRole)
    {
        // Outside the hierarchy: public comments are the site admin's, and escalation reaches
        // into any club regardless of standing there.
        Assert.True(CommentModerationAuthority.MayRemove(true, null, CommunityPermission.None,
            authorRole));
    }

    // ----- muting: same ladder, but site tools stop at the door --------------------------------

    [Theory]
    [InlineData(CommunityRole.Admin, true)]
    [InlineData(CommunityRole.Member, true)]
    [InlineData(CommunityRole.Banned, true)]
    [InlineData(CommunityRole.Creator, false)]
    public void TheCreatorMutesEveryoneBelowThemselves(CommunityRole? targetRole, bool expected)
    {
        Assert.Equal(expected, CommentModerationAuthority.MayMute(CommunityRole.Creator,
            CommunityPermission.None, targetRole));
    }

    [Theory]
    [InlineData(CommunityRole.Member, true)]
    [InlineData(CommunityRole.Admin, false)] // admins cannot mute admins
    [InlineData(CommunityRole.Creator, false)]
    public void AnAdminWithTheFlagMutesMembersOnly(CommunityRole? targetRole, bool expected)
    {
        Assert.Equal(expected, CommentModerationAuthority.MayMute(CommunityRole.Admin, Kit, targetRole));
    }

    [Fact]
    public void NobodyMutesSomeoneWithNoMembershipRow()
    {
        // You cannot take the mic from someone who is not in the room.
        Assert.False(CommentModerationAuthority.MayMute(CommunityRole.Creator, CommunityPermission.All,
            null));
    }

    [Fact]
    public void MuteAuthorityHasNoSiteAdminPath()
    {
        // The method takes no site-admin flag at all: the site admin's tools are removal and the
        // account lock, never a community mute. A site admin who is not in the club is a null
        // actor role here, and a null actor mutes nobody.
        Assert.False(CommentModerationAuthority.MayMute(null, CommunityPermission.All,
            CommunityRole.Member));
    }

    // ----- routing ----------------------------------------------------------------------------

    [Theory]
    [InlineData(CommentReportReason.HateOrDiscrimination, true)]
    [InlineData(CommentReportReason.ThreatsOrHarassment, true)]
    [InlineData(CommentReportReason.SpamOrAdvertising, false)]
    [InlineData(CommentReportReason.OffTopic, false)]
    [InlineData(CommentReportReason.WrongInformation, false)]
    [InlineData(CommentReportReason.JustWantAttention, false)] // site-ONLY is not "escalates" — it never had a community desk
    public void HateAndThreatsEscalateAndNothingElseDoes(CommentReportReason reason, bool escalates)
    {
        Assert.Equal(escalates, CommentReportRouting.EscalatesToSite(reason));
    }

    [Fact]
    public void JustWantAttentionIsTheSiteAdminsAloneAndEveryOtherReasonReachesTheClub()
    {
        // The one reason outside the deal: a hello goes to the site admin and never fills a
        // club's queue. Everything else reaches the community's own moderators.
        Assert.True(CommentReportRouting.IsSiteOnly(CommentReportReason.JustWantAttention));
        Assert.False(CommentReportRouting.ReachesCommunity(CommentReportReason.JustWantAttention));
        Assert.True(CommentReportRouting.ReachesSiteFromCommunity(CommentReportReason.JustWantAttention));

        foreach (var reason in Enum.GetValues<CommentReportReason>()
                     .Where(r => r != CommentReportReason.JustWantAttention))
        {
            Assert.False(CommentReportRouting.IsSiteOnly(reason));
            Assert.True(CommentReportRouting.ReachesCommunity(reason));
        }
    }
}
