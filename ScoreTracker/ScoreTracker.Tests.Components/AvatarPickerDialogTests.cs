using AngleSharp.Dom;
using Bunit;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.Account;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The avatar picker's behavior: which tile opens selected, what search narrows, what the
///     twelve redrawn avatars offer, and what a pick reports back. The parent owns saving, so
///     these assert on the callback rather than on any command.
/// </summary>
public sealed class AvatarPickerDialogTests : ComponentTestBase
{
    private static readonly Uri P2Teddy = new("https://piuimages.arroweclip.se/avatars/p2/teddy.png");
    private static readonly Uri P2Jeanne = new("https://piuimages.arroweclip.se/avatars/p2/jeanne.png");
    private static readonly Uri P1Jeanne = new("https://piuimages.arroweclip.se/avatars/jeanne.png");
    private static readonly Uri P2Azura = new("https://piuimages.arroweclip.se/avatars/p2/azura.png");

    /// <summary>
    ///     Three avatars in catalog order. Jeanne is one of the twelve Phoenix 2 genuinely
    ///     redrew, so she carries two pictures; the other two carry one, which is the shape
    ///     158 of the 170 have.
    /// </summary>
    private static IReadOnlyList<AvatarRecord> Catalog()
    {
        return new[]
        {
            new AvatarRecord("Azura", new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
                new[] { new AvatarPictureRecord(P2Azura, new[] { MixEnum.Phoenix2 }) }),
            new AvatarRecord("Jeanne", new[] { MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2 },
                new[]
                {
                    new AvatarPictureRecord(P2Jeanne, new[] { MixEnum.Phoenix2 }),
                    new AvatarPictureRecord(P1Jeanne, new[] { MixEnum.XX, MixEnum.Phoenix })
                }),
            new AvatarRecord("TEDDY", new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
                new[] { new AvatarPictureRecord(P2Teddy, new[] { MixEnum.Phoenix2 }) })
        };
    }

    /// <summary>
    ///     A MudDialog renders its content through MudDialogProvider, not in its own place in the
    ///     tree, so the provider has to be a sibling or the component under test produces nothing
    ///     at all — an empty render rather than an error.
    /// </summary>
    private IRenderedFragment RenderDialog(Uri? current = null, Action<Uri>? onPicked = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AvatarPickerDialog>(1);
            builder.AddAttribute(2, nameof(AvatarPickerDialog.Avatars), Catalog());
            builder.AddAttribute(3, nameof(AvatarPickerDialog.Current), current ?? P2Teddy);
            builder.AddAttribute(4, nameof(AvatarPickerDialog.Visible), true);
            builder.AddAttribute(5, nameof(AvatarPickerDialog.OnPicked),
                EventCallback.Factory.Create<Uri>(this, u => onPicked?.Invoke(u)));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void ListsEveryAvatarInCatalogOrder()
    {
        var tiles = RenderDialog().FindAll(".av-tile .av-tname").Select(t => t.TextContent.Trim()).ToArray();

        Assert.Equal(new[] { "Azura", "Jeanne", "TEDDY" }, tiles);
    }

    [Fact]
    public void OpensOnTheAvatarAlreadyWorn()
    {
        // Never an empty panel: the first click should be a change, not an orientation step.
        var cut = RenderDialog(current: P2Teddy);

        Assert.Equal("TEDDY", cut.Find(".av-strip-name").TextContent.Trim());
        Assert.Contains("Wearing", cut.Find(".av-tile.av-on").TextContent);
    }

    [Fact]
    public async Task SearchNarrowsByNameCaseInsensitively()
    {
        var cut = RenderDialog();

        await cut.Find(".av-toolbar input").InputAsync(new ChangeEventArgs { Value = "jean" });

        var tiles = cut.FindAll(".av-tile .av-tname").Select(t => t.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "Jeanne" }, tiles);
        Assert.Contains("1 of 3", cut.Find(".av-count").TextContent);
    }

    [Fact]
    public async Task SearchWithNoMatchSaysSoRatherThanRenderingAnEmptyGrid()
    {
        var cut = RenderDialog();

        await cut.Find(".av-toolbar input").InputAsync(new ChangeEventArgs { Value = "zzz" });

        Assert.Empty(cut.FindAll(".av-tile"));
        Assert.NotNull(cut.Find(".av-empty"));
    }

    [Fact]
    public async Task OnlyAnAvatarWithASecondPictureOffersTheChooser()
    {
        var cut = RenderDialog();

        // TEDDY is one picture: a Phoenix frame is not a second version, so no chooser.
        Assert.Empty(cut.FindAll(".av-vbtn"));

        await cut.FindAll(".av-tile")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal(2, cut.FindAll(".av-vbtn").Count);
        Assert.Equal("Jeanne", cut.Find(".av-strip-name").TextContent.Trim());
    }

    [Fact]
    public async Task PickingReportsTheSelectedPicture()
    {
        Uri? picked = null;
        var cut = RenderDialog(onPicked: u => picked = u);

        await cut.FindAll(".av-tile")[0].ClickAsync(new MouseEventArgs());
        await cut.Find(".av-strip button.mud-button-filled").ClickAsync(new MouseEventArgs());

        Assert.Equal(P2Azura, picked);
    }

    [Fact]
    public async Task PickingASecondVersionReportsThatPictureNotTheFirst()
    {
        // The whole point of the chooser: a player who wants Phoenix's Jeanne must not silently
        // get Phoenix 2's.
        Uri? picked = null;
        var cut = RenderDialog(onPicked: u => picked = u);

        await cut.FindAll(".av-tile")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".av-vbtn")[1].ClickAsync(new MouseEventArgs());
        await cut.Find(".av-strip button.mud-button-filled").ClickAsync(new MouseEventArgs());

        Assert.Equal(P1Jeanne, picked);
    }

    [Fact]
    public async Task SelectingAnotherAvatarResetsToItsFirstPicture()
    {
        // Variant index is positional, so carrying it across a selection would show avatar B's
        // second picture — or throw, for the 158 avatars that have only one.
        Uri? picked = null;
        var cut = RenderDialog(onPicked: u => picked = u);

        await cut.FindAll(".av-tile")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".av-vbtn")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".av-tile")[2].ClickAsync(new MouseEventArgs());
        await cut.Find(".av-strip button.mud-button-filled").ClickAsync(new MouseEventArgs());

        Assert.Equal(P2Teddy, picked);
    }

    [Fact]
    public void AvailabilityShowsEveryMixWithTheAbsentOnesMarkedOff()
    {
        // Azura is Phoenix and Phoenix 2 only, so XX renders present-but-off rather than absent:
        // "not in XX" is information a player wants, and a missing chip cannot say it.
        var cut = RenderDialog(current: P2Azura);

        var tags = cut.FindAll(".av-mixtag");
        Assert.Equal(3, tags.Count);
        Assert.Single(cut.FindAll(".av-mixtag.av-off"));
        Assert.Contains(MixEnum.XX.GetName(), cut.Find(".av-mixtag.av-off").TextContent);
    }
}
