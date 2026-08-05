using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Web.Components.Rivals;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The reverse list. Its add-back button answers to the same add-time gate the picker does
///     (docs/design/rivals.md D9) — somebody's arrow at you is not a basis for yours at them — so
///     the row only offers it where the command would actually succeed.
/// </summary>
public sealed class RivalsOfMeListTests : ComponentTestBase
{
    private const string AddLabel = "Add as rival";

    private static readonly Guid PublicStranger = Guid.NewGuid();

    private static RivalOfMeRecord Rival(Guid userId, string name, bool isPublic = true,
        bool sharesCommunity = false, bool isMutual = false) =>
        new(Guid.NewGuid(), userId, name, new Uri("https://piu.test/avatar.png"), isPublic,
            sharesCommunity, isMutual, DateTimeOffset.Parse("2026-07-01T00:00:00Z"));

    private IRenderedComponent<RivalsOfMeList> Render(IReadOnlyList<RivalOfMeRecord> rivals,
        Action<RivalOfMeRecord>? onAdd = null) =>
        RenderComponent<RivalsOfMeList>(p => p
            .Add(c => c.Rivals, rivals)
            .Add(c => c.Blocked, Array.Empty<BlockedPlayerRecord>())
            .Add(c => c.OnAdd, onAdd ?? (_ => { })));

    private static IEnumerable<string> ButtonLabels(IRenderedComponent<RivalsOfMeList> cut) =>
        cut.FindAll("button").Select(b => b.TextContent.Trim());

    [Fact]
    public void APublicPlayerWhoRivalsYouCanBeRivalledBackFromTheRow()
    {
        var cut = Render(new[] { Rival(PublicStranger, "ALICE") });

        Assert.Contains(AddLabel, ButtonLabels(cut));
    }

    [Fact]
    public void AddingBackHandsTheWholeRowUpSoTheCallerCanNameThePlayer()
    {
        RivalOfMeRecord? added = null;
        var cut = Render(new[] { Rival(PublicStranger, "ALICE") }, r => added = r);

        cut.FindAll("button").First(b => b.TextContent.Trim() == AddLabel).Click();

        Assert.Equal(PublicStranger, added?.UserId);
    }

    /// <summary>
    ///     The row already says "you rival them too"; offering the add again would resolve to the
    ///     edge that sentence is describing.
    /// </summary>
    [Fact]
    public void SomebodyYouAlreadyRivalIsNotOfferedAgain()
    {
        var cut = Render(new[] { Rival(PublicStranger, "ALICE", isMutual: true) });

        Assert.DoesNotContain(AddLabel, ButtonLabels(cut));
    }

    /// <summary>
    ///     A private player can rival a public one (D13), so this row exists — but the reverse add
    ///     has no basis, and a button that comes back "not available" is worse than none.
    /// </summary>
    [Fact]
    public void APrivateStrangerIsListedButNotOffered()
    {
        var cut = Render(new[] { Rival(Guid.NewGuid(), "GHOSTLY", false) });

        Assert.Contains("GHOSTLY", cut.Markup);
        Assert.DoesNotContain(AddLabel, ButtonLabels(cut));
    }

    /// <summary>A shared community is a basis of its own, private account or not (D9c).</summary>
    [Fact]
    public void APrivateClubmateIsOffered()
    {
        var cut = Render(new[] { Rival(Guid.NewGuid(), "CLUBMATE", false, true) });

        Assert.Contains(AddLabel, ButtonLabels(cut));
    }

    /// <summary>Revocation is the point of this list and stays on every row regardless.</summary>
    [Fact]
    public void EveryRowKeepsRemoveAndBlock()
    {
        var cut = Render(new[]
        {
            Rival(PublicStranger, "ALICE"),
            Rival(Guid.NewGuid(), "GHOSTLY", false),
            Rival(Guid.NewGuid(), "MUTUAL", isMutual: true)
        });

        var labels = ButtonLabels(cut).ToArray();
        Assert.Equal(3, labels.Count(l => l == "Remove"));
        Assert.Equal(3, labels.Count(l => l == "Block"));
    }
}
