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
}
