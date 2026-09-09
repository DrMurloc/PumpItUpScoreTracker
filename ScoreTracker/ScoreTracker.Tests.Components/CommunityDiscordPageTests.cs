using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Moq;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.Communities;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The three server states the page has to draw, and the two failures Discord itself reports
///     to nobody — a role above the bot and a role that no longer exists.
/// </summary>
public sealed class CommunityDiscordPageTests : ComponentTestBase
{
    private const ulong Guild = 900;
    private static readonly Guid CommunityId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private CommunityDiscordView _view = Linked();

    private static CommunityDiscordServerRecord Server(string name = "Arrow Eclipse") =>
        new(CommunityId, Guild, name, Now);

    private static CommunityDiscordView Linked(
        IReadOnlyList<CommunityTitleRoleView>? mappings = null, bool canManage = true) =>
        new(CommunityId, Name.From("Arrow Eclipse"), Server(), true, true, canManage, true,
            mappings ?? new[]
            {
                new CommunityTitleRoleView("[P.B] BRONZE", "PUMBILITY Total", 1, "@Bronze", null, null, 4),
                new CommunityTitleRoleView("[PHOENIX] SINGLE BOSS BREAKER", null, 3, "@Boss Breaker", null,
                    null, 3)
            },
            41, 23, Now);

    private void Given(CommunityDiscordView view,
        IReadOnlyList<DiscordRoleChangeRecord>? pending = null)
    {
        _view = view;
        Mediator.Setup(m => m.Send(It.IsAny<GetCommunityDiscordQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _view);
        Mediator.Setup(m => m.Send(It.IsAny<GetCommunityDiscordRolesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BotGuildRole(1, "@Bronze", null, null),
                new BotGuildRole(9, "@Moderator", null, BotRoleBlockedReason.AboveBot)
            });
        Mediator.Setup(m => m.Send(It.IsAny<GetCommunityDiscordPreviewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending ?? Array.Empty<DiscordRoleChangeRecord>());
    }

    private IRenderedComponent<CommunityDiscord> Render()
    {
        // A SupplyParameterFromQuery parameter cannot be handed in directly; the page reads it
        // off the URL like the real one does.
        this.RenderInteractive();
        Services.GetRequiredService<FakeNavigationManager>()
            .NavigateTo("/Community/Discord?CommunityName=Arrow%20Eclipse");
        return RenderComponent<CommunityDiscord>();
    }

    [Fact]
    public void ALinkedServerShowsItsNameAndTheMappings()
    {
        Given(Linked());

        var markup = Render().Markup;

        Assert.Contains("Arrow Eclipse", markup);
        Assert.Contains("[P.B] BRONZE", markup);
        Assert.Contains("@Bronze", markup);
        Assert.Contains("4 members", markup);
    }

    /// <summary>
    ///     A pumbility ladder hands out one role; the chip is the only place the page says so.
    /// </summary>
    [Fact]
    public void APumbilityGroupIsMarkedHighestOnlyAndOtherTitlesAreNot()
    {
        Given(Linked());

        var markup = Render().Markup;

        Assert.Contains("Highest only", markup);
        Assert.Contains("PUMBILITY — combined", markup);
        Assert.Contains("Everything else", markup);
    }

    /// <summary>
    ///     The failure that generates every support message: Discord refuses a role above the bot
    ///     and tells the player nothing, so the page has to say it twice — the health list and the
    ///     row itself.
    /// </summary>
    [Fact]
    public void ARoleAboveTheBotIsCalledOutWithTheFix()
    {
        Given(Linked(new[]
        {
            new CommunityTitleRoleView("[P.B] DIAMOND", "PUMBILITY Total", 9, "@Diamond", null,
                BotRoleBlockedReason.AboveBot, 2)
        }));

        var markup = Render().Markup;

        Assert.Contains("above the bot", markup);
        Assert.Contains("drag the PIU Scores role above them", markup);
    }

    /// <summary>
    ///     The state every server invited before this feature is in, and the one the canary caught
    ///     on its first real run. It is NOT a hierarchy problem, so the page must not send an admin
    ///     off to reorder roles — nothing there can fix a permission the bot was never given.
    /// </summary>
    [Fact]
    public void ABotWithNoManageRolesIsToldToReAddRatherThanReorder()
    {
        Given(_view with
        {
            BotCanManageRoles = false,
            Mappings = new[]
            {
                new CommunityTitleRoleView("[P.B] BRONZE", "PUMBILITY Total", 1, "@Bronze", null,
                    BotRoleBlockedReason.BotCannotManageRoles, 4)
            }
        });

        var markup = Render().Markup;

        Assert.Contains("no permission to manage roles", markup);
        Assert.Contains("Re-add the bot", markup);
        Assert.DoesNotContain("drag the PIU Scores role above them", markup);
    }

    [Fact]
    public void AMappingPointingAtADeletedRoleSaysSo()
    {
        Given(Linked(new[]
        {
            new CommunityTitleRoleView("[P.B] GOLD", "PUMBILITY Total", 2, null, null, null, 0)
        }));

        var markup = Render().Markup;

        Assert.Contains("no longer exists in the server", markup);
    }

    [Fact]
    public void WithNoServerAnAdminIsToldWhichCommandToRun()
    {
        Given(_view with { Server = null, BotIsInServer = false, Mappings = Array.Empty<CommunityTitleRoleView>() });

        var markup = Render().Markup;

        Assert.Contains("/piu link-server", markup);
        Assert.Contains("isn't the same as this community's server", markup);
    }

    /// <summary>
    ///     Designating a server is done as yourself in Discord, so the page checks up front rather
    ///     than letting an admin discover it mid-command.
    /// </summary>
    [Fact]
    public void AnAdminWithNoDiscordLinkedIsSentToTheirAccountFirst()
    {
        Given(_view with
        {
            Server = null, BotIsInServer = false, ViewerHasDiscordLinked = false,
            Mappings = Array.Empty<CommunityTitleRoleView>()
        });

        var markup = Render().Markup;

        Assert.Contains("Link your own Discord account first", markup);
        Assert.DoesNotContain("/piu link-server", markup);
    }

    [Fact]
    public void AMemberSeesNoControls()
    {
        Given(Linked(canManage: false));

        var markup = Render().Markup;

        Assert.DoesNotContain("Add a role", markup);
        Assert.DoesNotContain("Change server", markup);
        Assert.DoesNotContain("Next check will change", markup);
    }

    [Fact]
    public void ThePreviewNamesWhoChangesAndWhy()
    {
        Given(Linked(), new[]
        {
            new DiscordRoleChangeRecord(Guid.NewGuid(), Name.From("RAINBOW"),
                new Uri("https://piuscores.arroweclip.se/i.png"), new[] { "[P.B] GOLD" },
                new[] { "[P.B] SILVER" }, null)
        });

        var markup = Render().Markup;

        Assert.Contains("RAINBOW", markup);
        Assert.Contains("[P.B] GOLD", markup);
        Assert.Contains("[P.B] SILVER", markup);
    }

    [Fact]
    public void AnEmptyPreviewSaysNothingIsChanging()
    {
        Given(Linked());

        var markup = Render().Markup;

        Assert.Contains("Nothing to change", markup);
    }

    /// <summary>
    ///     Changing the server takes every role back first, so it asks rather than doing it on one
    ///     click (owner call). The confirmation body lives in a MudDialog, which needs a provider
    ///     this tree has none of — so what is asserted is the half that matters: the click alone
    ///     unlinks nothing.
    /// </summary>
    [Fact]
    public async Task ChangingTheServerDoesNotUnlinkOnTheFirstClick()
    {
        Given(Linked());
        var page = Render();

        await page.Find("button:has(span:contains('Change server'))").ClickAsync(new());

        Mediator.Verify(m => m.Send(It.IsAny<UnlinkCommunityDiscordServerCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
