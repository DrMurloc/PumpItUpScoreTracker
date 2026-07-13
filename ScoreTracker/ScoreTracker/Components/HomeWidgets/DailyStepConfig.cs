using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

/// <summary>Daily Step widget config (public contract via export/import, D19). Mix scope only.</summary>
public sealed record DailyStepConfig
{
    public MixEnum? Mix { get; set; }
}
