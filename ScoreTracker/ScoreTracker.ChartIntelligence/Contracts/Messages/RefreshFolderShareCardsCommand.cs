using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

// Regenerates every folder's community share card (the per-folder og:image). The
// publisher — Web's RecurringJobRunner — resolves the presentation theme from
// MixThemes, the single palette source; the consuming saga stays palette-blind.
// The transport is in-memory, so the theme payload rides the message fine.
[ExcludeFromCodeCoverage]
public sealed record RefreshFolderShareCardsCommand(
    FolderShareCardTheme Theme,
    MixEnum Mix = MixEnum.Phoenix)
{
}
