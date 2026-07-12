using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Application;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.HomePage.Contracts.Commands;
using ScoreTracker.HomePage.Contracts.Exceptions;
using ScoreTracker.HomePage.Contracts.Queries;
using ScoreTracker.HomePage.Domain;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class HomePageLayoutSagaTests
{
    [Fact]
    public async Task FirstCreatedPageBecomesDefaultAtOrdinalZero()
    {
        var ctx = new LayoutContext();

        var page = await ctx.Saga.Handle(new CreateHomePageCommand("Home"), CancellationToken.None);

        Assert.True(page.IsDefault);
        Assert.Equal(0, page.Ordinal);
        ctx.Repository.Verify(r => r.CreatePage(ctx.UserId,
            It.Is<HomePageRecord>(p => p.IsDefault && p.Ordinal == 0 && p.Name == "Home"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SecondPageAppendsAndIsNotDefault()
    {
        var ctx = new LayoutContext();
        ctx.WithPage("Home", isDefault: true);

        var page = await ctx.Saga.Handle(new CreateHomePageCommand("Session Day"), CancellationToken.None);

        Assert.False(page.IsDefault);
        Assert.Equal(1, page.Ordinal);
    }

    [Fact]
    public async Task PageCapIsEnforcedInTheHandler()
    {
        var ctx = new LayoutContext();
        for (var i = 0; i < HomePageRecord.MaxPagesPerUser; i++) ctx.WithPage($"Page {i}", isDefault: i == 0);

        await Assert.ThrowsAsync<HomePageCapReachedException>(() =>
            ctx.Saga.Handle(new CreateHomePageCommand("One Too Many"), CancellationToken.None));
    }

    [Fact]
    public async Task WidgetCapIsEnforcedInTheHandler()
    {
        var ctx = new LayoutContext();
        var pageId = ctx.WithPage("Home", isDefault: true,
            widgets: Enumerable.Range(0, HomePageRecord.MaxWidgetsPerPage)
                .Select(i => Widget($"w{i}", i)).ToArray());

        await Assert.ThrowsAsync<HomePageCapReachedException>(() =>
            ctx.Saga.Handle(new AddHomePageWidgetCommand(pageId, "pumbility", "1x1", null, "{}", 1),
                CancellationToken.None));
    }

    [Fact]
    public async Task AddedWidgetAppendsAtTheEndOfAutoFlowOrder()
    {
        var ctx = new LayoutContext();
        var pageId = ctx.WithPage("Home", isDefault: true, widgets: new[] { Widget("weekly", 0) });

        var widget = await ctx.Saga.Handle(
            new AddHomePageWidgetCommand(pageId, "pumbility", "2x1", "Doubles push", "{}", 1),
            CancellationToken.None);

        Assert.Equal(1, widget.Ordinal);
        ctx.Repository.Verify(r => r.AddWidget(pageId,
            It.Is<HomePageWidgetRecord>(w => w.WidgetType == "pumbility" && w.Ordinal == 1
                                             && w.SizePreset == "2x1" && w.Title == "Doubles push"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MovingAWidgetRewritesTheWholePagesOrdinals()
    {
        var ctx = new LayoutContext();
        var a = Widget("a", 0);
        var b = Widget("b", 1);
        var c = Widget("c", 2);
        ctx.WithPage("Home", isDefault: true, widgets: new[] { a, b, c });

        await ctx.Saga.Handle(new MoveHomePageWidgetCommand(c.Id, 0), CancellationToken.None);

        var expected = new[] { (c.Id, 0), (a.Id, 1), (b.Id, 2) };
        ctx.Repository.Verify(r => r.SetWidgetOrdinals(
            It.Is<IReadOnlyList<(Guid WidgetId, int Ordinal)>>(o => OrdinalsAre(o, expected)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovingAWidgetCompactsTheRemainingOrdinals()
    {
        var ctx = new LayoutContext();
        var a = Widget("a", 0);
        var b = Widget("b", 1);
        var c = Widget("c", 2);
        ctx.WithPage("Home", isDefault: true, widgets: new[] { a, b, c });

        await ctx.Saga.Handle(new RemoveHomePageWidgetCommand(b.Id), CancellationToken.None);

        ctx.Repository.Verify(r => r.RemoveWidget(b.Id, It.IsAny<CancellationToken>()), Times.Once);
        var expected = new[] { (a.Id, 0), (c.Id, 1) };
        ctx.Repository.Verify(r => r.SetWidgetOrdinals(
            It.Is<IReadOnlyList<(Guid WidgetId, int Ordinal)>>(o => OrdinalsAre(o, expected)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletingTheDefaultPagePromotesTheNextByTabOrder()
    {
        var ctx = new LayoutContext();
        var home = ctx.WithPage("Home", isDefault: true);
        var second = ctx.WithPage("Session Day", isDefault: false);
        var third = ctx.WithPage("Progress", isDefault: false);

        await ctx.Saga.Handle(new DeleteHomePageCommand(home), CancellationToken.None);

        ctx.Repository.Verify(r => r.DeletePage(home, It.IsAny<CancellationToken>()), Times.Once);
        var expected = new[] { (second, 0, true), (third, 1, false) };
        ctx.Repository.Verify(r => r.UpdatePageOrder(
            It.Is<IReadOnlyList<(Guid PageId, int Ordinal, bool IsDefault)>>(o => PageOrderIs(o, expected)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForeignOrStaleIdsReadAsNotFound()
    {
        var ctx = new LayoutContext();
        ctx.WithPage("Home", isDefault: true);

        await Assert.ThrowsAsync<HomePageNotFoundException>(() =>
            ctx.Saga.Handle(new RenameHomePageCommand(Guid.NewGuid(), "Nope"), CancellationToken.None));
        await Assert.ThrowsAsync<HomePageNotFoundException>(() =>
            ctx.Saga.Handle(new RemoveHomePageWidgetCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ShapeValidationRejectsOversizedFields()
    {
        var ctx = new LayoutContext();
        var pageId = ctx.WithPage("Home", isDefault: true);

        await Assert.ThrowsAsync<HomePageValidationException>(() =>
            ctx.Saga.Handle(new RenameHomePageCommand(pageId, new string('x', 65)), CancellationToken.None));
        await Assert.ThrowsAsync<HomePageValidationException>(() =>
            ctx.Saga.Handle(new AddHomePageWidgetCommand(pageId, "pumbility", "1x1", null,
                new string('{', 2001), 1), CancellationToken.None));
    }

    [Fact]
    public async Task QueryReturnsThePagesInTabOrder()
    {
        var ctx = new LayoutContext();
        ctx.WithPage("Home", isDefault: true);
        ctx.WithPage("Session Day", isDefault: false);

        var pages = await ctx.Saga.Handle(new GetMyHomePagesQuery(), CancellationToken.None);

        Assert.Equal(2, pages.Count);
        Assert.Equal("Home", pages[0].Name.ToString());
    }

    [Fact]
    public async Task ReplaceSwapsTheWholeWidgetListWithFreshOrdinals()
    {
        var ctx = new LayoutContext();
        var pageId = ctx.WithPage("Home", isDefault: true, widgets: new[] { Widget("old", 0) });

        await ctx.Saga.Handle(new ReplaceHomePageWidgetsCommand(pageId, new[]
        {
            new HomePageWidgetSpec("pumbility", null, "2x1", "{}", 1),
            new HomePageWidgetSpec("weekly-challenge", "My board", "1x1", "{}", 1)
        }), CancellationToken.None);

        ctx.Repository.Verify(r => r.ReplaceWidgets(pageId,
            It.Is<IReadOnlyList<HomePageWidgetRecord>>(w =>
                w.Count == 2 && w[0].WidgetType == "pumbility" && w[0].Ordinal == 0
                && w[1].WidgetType == "weekly-challenge" && w[1].Ordinal == 1
                && w[1].Title == "My board"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceEnforcesTheWidgetCap()
    {
        var ctx = new LayoutContext();
        var pageId = ctx.WithPage("Home", isDefault: true);
        var specs = Enumerable.Range(0, HomePageRecord.MaxWidgetsPerPage + 1)
            .Select(i => new HomePageWidgetSpec($"w{i}", null, "1x1", "{}", 1))
            .ToArray();

        await Assert.ThrowsAsync<HomePageCapReachedException>(() =>
            ctx.Saga.Handle(new ReplaceHomePageWidgetsCommand(pageId, specs), CancellationToken.None));
    }

    private static HomePageWidgetRecord Widget(string type, int ordinal)
    {
        return new HomePageWidgetRecord(Guid.NewGuid(), type, null, ordinal, "1x1", "{}", 1);
    }

    private static bool OrdinalsAre(IReadOnlyList<(Guid WidgetId, int Ordinal)> actual,
        IReadOnlyList<(Guid, int)> expected)
    {
        return actual.Count == expected.Count && actual.Zip(expected).All(p => p.First == p.Second);
    }

    private static bool PageOrderIs(IReadOnlyList<(Guid PageId, int Ordinal, bool IsDefault)> actual,
        IReadOnlyList<(Guid, int, bool)> expected)
    {
        return actual.Count == expected.Count && actual.Zip(expected).All(p => p.First == p.Second);
    }

    private sealed class LayoutContext
    {
        private readonly List<HomePageRecord> _pages = new();

        public LayoutContext()
        {
            var user = new UserBuilder().Build();
            UserId = user.Id;
            var currentUser = new Mock<ICurrentUserAccessor>();
            currentUser.SetupGet(c => c.User).Returns(user);
            Repository.Setup(r => r.GetPages(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _pages.ToArray());
            Saga = new HomePageLayoutSaga(Repository.Object, currentUser.Object);
        }

        public Guid UserId { get; }
        public Mock<IHomePageRepository> Repository { get; } = new();
        public HomePageLayoutSaga Saga { get; }

        public Guid WithPage(string name, bool isDefault, HomePageWidgetRecord[]? widgets = null)
        {
            var page = new HomePageRecord(Guid.NewGuid(), name, _pages.Count, isDefault, null,
                widgets ?? Array.Empty<HomePageWidgetRecord>());
            _pages.Add(page);
            return page.Id;
        }
    }
}
