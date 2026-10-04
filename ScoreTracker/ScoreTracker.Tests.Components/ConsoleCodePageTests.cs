using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.CommunityTools;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Code section's API snippets, which a maker copies as the starting point of a tool. Each one
///     authenticates with the tool's Bearer key, so it has to read players by id — a tool key asking
///     for <c>players/me</c> gets a 400 — and it has to follow <c>next</c> as the absolute URL the
///     API already sends, because a URL built around it is malformed from page two on.
/// </summary>
public sealed class ConsoleCodePageTests : ComponentTestBase
{
    private static readonly Guid ToolId = Guid.Parse("cccccccc-7777-7777-7777-777777777777");
    private static readonly Guid OwnerId = Guid.Parse("ffffffff-6666-6666-6666-666666666666");

    public ConsoleCodePageTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(OwnerId, Name.From("TUSA"), true, null,
            new Uri("https://piu.test/a.png"), null));

        var tool = new ToolRecord(ToolId, OwnerId, "TUSA", "Planner", null, "https://planner.example",
            ToolVisibility.Private, true, WebhookMode.None, null, new[] { MixEnum.Phoenix2 }, 0,
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero), null, null, null, null, false, false, null, null, null, true, null, null,
            ToolKind.Integrated, true, false);
        Mediator.Setup(m => m.Send(It.IsAny<GetMyToolsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tool });
        Mediator.Setup(m => m.Send(It.IsAny<GetToolCodeSamplesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolCodeContext("ab12", null, null, new[] { MixEnum.Phoenix2 }));
    }

    private async Task<string> ApiSnippetIn(string language)
    {
        var page = RenderComponent<ConsoleCode>(p => p.Add(x => x.ToolId, ToolId));
        await Chip(page, language).ClickAsync(new MouseEventArgs());
        return page.Find("pre").TextContent;
    }

    private static IElement Chip(IRenderedFragment page, string label)
    {
        return page.FindAll(".mud-chip").First(c => c.TextContent.Trim() == label);
    }

    [Theory]
    [InlineData("C#")]
    [InlineData("Java")]
    [InlineData("Python")]
    [InlineData("TypeScript")]
    public async Task TheApiSnippetListsTheToolsPlayersAndReadsEachOneById(string language)
    {
        var snippet = await ApiSnippetIn(language);

        Assert.Contains("Bearer", snippet);
        Assert.Contains("piu_scores_live_…ab12", snippet);
        Assert.DoesNotContain("players/me", snippet);
        Assert.Contains("api/v2/players?limit=500", snippet);
        Assert.Contains("/scores?mix=Phoenix2", snippet);
    }

    [Theory]
    [InlineData("C#")]
    [InlineData("Java")]
    [InlineData("Python")]
    [InlineData("TypeScript")]
    public async Task TheApiSnippetFollowsNextAsTheUrlItAlreadyIs(string language)
    {
        var snippet = await ApiSnippetIn(language);

        var readsNext = snippet.Split('\n')
            .Where(line => line.Contains("next", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(readsNext);
        // Concatenation, a template or the site's own address on a line that reads `next` is a URL
        // being built around one that is already absolute.
        Assert.All(readsNext, line =>
        {
            Assert.DoesNotContain("+", line);
            Assert.DoesNotContain("${", line);
            Assert.DoesNotContain("$\"", line);
            Assert.DoesNotContain("f\"", line);
            Assert.DoesNotContain("http://localhost", line);
        });
    }
}
