using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Route allowlist for pre-XX legacy mixes (docs/design/legacy-mixes.md). Most pages
///     were built assuming a Phoenix-family (or at least XX) mix — under an older mix
///     they range from quietly empty to throwing (TitleSaga has no title list for
///     The Prex 3, and never will). Until a page is validated for old mixes it renders
///     the construction panel instead. XX is deliberately NOT gated: it predates this
///     feature and keeps whatever behavior it has today.
///     Validating a page = field-test it under a pre-XX mix, then add its first route
///     segment here.
/// </summary>
public static class LegacyMixGate
{
    private static readonly HashSet<string> ReadySegments = new(StringComparer.OrdinalIgnoreCase)
    {
        // Validated during the legacy-mixes field tests:
        "Charts", // browse + record grid
        "Chart", // details + recording
        "Record", // ChartDetails alias route
        "UploadXXScores", // mix-aware CSV upload
        "TierLists", // renders its own coming-soon state for legacy mixes
        // Mix-independent surfaces:
        "Login",
        "Logout",
        "Welcome",
        "Account",
        "Dev"
    };

    public static bool IsGated(MixEnum mix, string path)
    {
        if (!mix.UsesLegacyScoring() || mix == MixEnum.XX) return false;
        var firstSegment = path.Trim('/').Split('/')[0];
        if (firstSegment.Length == 0) return true; // home (WhatShouldIPlay) is not legacy-ready
        return !ReadySegments.Contains(firstSegment);
    }
}
