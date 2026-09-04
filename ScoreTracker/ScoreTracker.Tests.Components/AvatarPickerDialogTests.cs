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
    private static readonly Uri P1Teddy = new("https://piuimages.arroweclip.se/avatars/teddy-p1.png");
    private static readonly Uri P2Jeanne = new("https://piuimages.arroweclip.se/avatars/p2/jeanne.png");
    private static readonly Uri P1Jeanne = new("https://piuimages.arroweclip.se/avatars/jeanne.png");
    private static readonly Uri P2Azura = new("https://piuimages.arroweclip.se/avatars/p2/azura.png");

    /// <summary>
    ///     Three avatars in catalog order. Jeanne is one of the twelve Phoenix 2 genuinely
    ///     redrew, so she carries two pictures; the other two carry one, which is the shape
    ///     158 of the 170 have.
    ///     <para>
    ///         TEDDY's single picture is served at BOTH mirror paths, which is the ordinary case
    ///         — 145 avatars are the same art under two prefixes, and the Phoenix one is what
    ///         most live accounts wear.
    ///     </para>
    /// </summary>
    private static IReadOnlyList<AvatarRecord> Catalog()
    {
        return new[]
        {
            new AvatarRecord("Azura", new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
                new[] { new AvatarPictureRecord(P2Azura, new[] { MixEnum.Phoenix2 }, new[] { P2Azura }) }),
            new AvatarRecord("Jeanne", new[] { MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2 },
                new[]
                {
                    new AvatarPictureRecord(P2Jeanne, new[] { MixEnum.Phoenix2 }, new[] { P2Jeanne }),
                    new AvatarPictureRecord(P1Jeanne, new[] { MixEnum.XX, MixEnum.Phoenix }, new[] { P1Jeanne })
                }),
            new AvatarRecord("TEDDY", new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
                new[]
                {
                    new AvatarPictureRecord(P2Teddy, new[] { MixEnum.Phoenix, MixEnum.Phoenix2 },
                        new[] { P2Teddy, P1Teddy })
                })
        };
    }

    /// <summary>
    ///     A MudDialog renders its content through MudDialogProvider, not in its own place in the
    ///     tree, so the provider has to be a sibling or the component under test produces nothing
    ///     at all — an empty render rather than an error.
    /// </summary>
    private IRenderedFragment RenderDialog(Uri? current = null, Action<Uri>? onPicked = null)
    {
        return RenderDialog(out _, current, onPicked);
    }

    private IRenderedFragment RenderDialog(out IRenderedComponent<AvatarPickerDialog> dialog,
        Uri? current = null, Action<Uri>? onPicked = null)
    {
        var fragment = Render(builder =>
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
        dialog = fragment.FindComponent<AvatarPickerDialog>();
        return fragment;
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
        Assert.Contains("Avatar In Use", cut.Find(".av-tile.av-on").TextContent);
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

    [Fact]
    public void RecognisesTheMirrorPathTheOtherMixServesTheSamePictureAt()
    {
        // The catalog stores one canonical url per picture, but each mix mirrors that picture
        // under its own prefix and a player wears whichever their importer wrote. Matching the
        // canonical url alone left three quarters of live accounts unrecognised.
        var cut = RenderDialog(current: P1Teddy);

        Assert.Equal("TEDDY", cut.Find(".av-strip-name").TextContent.Trim());
        Assert.Contains("Avatar In Use", cut.Find(".av-tile.av-on").TextContent);
    }

    [Theory]
    [InlineData("https://piuimages.arroweclip.se/avatars%2fteddy-p1.png")]
    [InlineData("https://piuimages.arroweclip.se//avatars/teddy-p1.png")]
    public void RecognisesTheMalformedUrlsOlderWritesLeftBehind(string worn)
    {
        // A percent-encoded slash and a doubled slash both exist in production. The CDN serves
        // them, so they render correctly and were never noticed — but they match nothing
        // verbatim, and the player would see no badge at all.
        var cut = RenderDialog(current: new Uri(worn));

        Assert.Equal("TEDDY", cut.Find(".av-strip-name").TextContent.Trim());
    }

    [Fact]
    public void AnUncataloguedAvatarSelectsNothingRatherThanBadgingTheWrongTile()
    {
        var cut = RenderDialog(current: new Uri("https://piuimages.arroweclip.se/avatars/unknown.png"));

        Assert.Empty(cut.FindAll(".av-now"));
        Assert.Equal("Azura", cut.Find(".av-strip-name").TextContent.Trim());
    }

    [Fact]
    public async Task PickingTwiceInOneSessionWorks()
    {
        // The parent closes the dialog but never reloads the page, so this instance survives to
        // be reopened. A saving latch that was never cleared held the button dead forever.
        // Queries go through the fragment, never the component handle: a MudDialog renders its
        // content into the provider, so the component's own subtree is empty.
        var picks = new List<Uri>();
        var cut = RenderDialog(out _, onPicked: picks.Add);

        await cut.FindAll(".av-tile")[0].ClickAsync(new MouseEventArgs());
        await cut.Find(".av-strip button.mud-button-filled").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".av-tile")[1].ClickAsync(new MouseEventArgs());
        await cut.Find(".av-strip button.mud-button-filled").ClickAsync(new MouseEventArgs());

        Assert.Equal(new[] { P2Azura, P2Jeanne }, picks);
    }

    [Fact]
    public void ChangingWhatIsWornMovesTheSelection()
    {
        // Current is a live parameter the parent rewrites after every pin and every Back to Auto.
        // Resolving it once per lifetime meant reopening showed the avatar just abandoned.
        var cut = RenderDialog(out var dialog, current: P2Azura);
        Assert.Equal("Azura", cut.Find(".av-strip-name").TextContent.Trim());

        dialog.SetParametersAndRender(p => p.Add(x => x.Current, P2Teddy));

        Assert.Equal("TEDDY", cut.Find(".av-strip-name").TextContent.Trim());
        Assert.Contains("Avatar In Use", cut.Find(".av-tile.av-on").TextContent);
    }

    [Fact]
    public void TheWornTileShowsTheArtYouAreWearingNotTheCanonicalCopy()
    {
        // Jeanne is one of the twelve with two genuinely different pictures. Badging the tile
        // "Avatar In Use" while rendering the other picture claims art the player does not have.
        var cut = RenderDialog(current: P1Jeanne);

        var worn = cut.Find(".av-tile.av-on img");
        Assert.Equal(P1Jeanne.ToString(), worn.GetAttribute("src"));
    }
}
