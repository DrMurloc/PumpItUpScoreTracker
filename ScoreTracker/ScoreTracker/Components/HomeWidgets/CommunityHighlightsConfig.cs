using System;
using System.Collections.Generic;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>
///     Community Highlights widget config (public contract via export/import, D19).
///     Empty <see cref="Communities" /> follows your non-regional crews (CH1 — World and your
///     country are opt-in). <see cref="IncludeOwnWins" /> defaults on (CH4).
/// </summary>
public sealed record CommunityHighlightsConfig
{
    public MixEnum? Mix { get; set; }

    public IReadOnlyList<string> Communities { get; set; } = Array.Empty<string>();

    public bool IncludeOwnWins { get; set; } = true;

    /// <summary>
    ///     Whether the feed also carries your rivals' wins (docs/design/rivals.md D38). Defaults
    ///     FALSE and must stay that way: an absent field on a dashboard somebody configured months
    ///     ago has to keep behaving exactly as it did, and silently adding rows to a shipped
    ///     widget is a content change nobody asked for.
    /// </summary>
    public bool IncludeRivals { get; set; }

    /// <summary>
    ///     Whether the community half runs at all. Defaults TRUE for the same reason
    ///     <see cref="IncludeRivals" /> defaults false — an absent field on an existing dashboard
    ///     must behave exactly as it did. A rivals-only feed needs this because an EMPTY
    ///     <see cref="Communities" /> already means "all my crews", so there was no way to say
    ///     "none of them".
    /// </summary>
    public bool IncludeCommunities { get; set; } = true;
}
