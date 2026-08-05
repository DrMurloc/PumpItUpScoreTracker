using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.Rivals;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The two pickers on the add-a-rival panel. Both run a query per keystroke, and
///     MudAutocomplete cancels the one in flight as soon as the next character arrives — so an
///     abandoned search is routine here and must not reach the user as an error.
/// </summary>
public sealed class AddRivalPanelTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();

    public AddRivalPanelTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<SearchRivalTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mediator.Setup(m => m.Send(It.IsAny<SearchRivalCandidatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalCandidateRecord>());
        Services.AddSingleton(_mediator.Object);
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedFragment RenderPanel() => Render(builder =>
    {
        builder.OpenComponent<CascadingValue<MixEnum>>(0);
        builder.AddAttribute(1, "Name", "Mix");
        builder.AddAttribute(2, "Value", MixEnum.Phoenix);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(inner =>
        {
            inner.OpenComponent<AddRivalPanel>(0);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    });

    private Func<string, CancellationToken, Task<IEnumerable<T>>> SearchFuncOf<T>(IRenderedFragment cut) =>
        cut.FindComponent<MudAutocomplete<T>>().Instance.SearchFunc!;

    /// <summary>
    ///     The reported crash: a cancelled SqlCommand does not surface as an
    ///     OperationCanceledException, so the guard keys on the token rather than the exception
    ///     type. Any failure carrying a cancelled token is the user's next keystroke.
    /// </summary>
    [Fact]
    public async Task ATagSearchAbandonedMidKeystrokeYieldsNothingRatherThanThrowing()
    {
        _mediator.Setup(m => m.Send(It.IsAny<SearchRivalTagsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "A severe error occurred on the current command. Operation cancelled by user."));
        var search = SearchFuncOf<string>(RenderPanel());
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        Assert.Empty(await search("ali", abandoned.Token));
    }

    [Fact]
    public async Task APlayerSearchAbandonedMidKeystrokeYieldsNothingRatherThanThrowing()
    {
        _mediator.Setup(m => m.Send(It.IsAny<SearchRivalCandidatesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cancelled mid-flight"));
        var search = SearchFuncOf<RivalCandidateRecord>(RenderPanel());
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        Assert.Empty(await search("ali", abandoned.Token));
    }

    /// <summary>
    ///     The other half of the guard, and the reason it filters on the token instead of
    ///     swallowing everything: a search that genuinely broke is still an error.
    /// </summary>
    [Fact]
    public async Task ASearchThatFailedOnItsOwnStillSurfaces()
    {
        _mediator.Setup(m => m.Send(It.IsAny<SearchRivalTagsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the boards are unreadable"));
        var search = SearchFuncOf<string>(RenderPanel());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => search("ali", CancellationToken.None));
    }

    [Fact]
    public async Task AnEmptyTermAsksNothingOfTheDatabase()
    {
        var cut = RenderPanel();

        Assert.Empty(await SearchFuncOf<string>(cut)("   ", CancellationToken.None));
        Assert.Empty(await SearchFuncOf<RivalCandidateRecord>(cut)("", CancellationToken.None));

        _mediator.Verify(m => m.Send(It.IsAny<SearchRivalTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<SearchRivalCandidatesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
