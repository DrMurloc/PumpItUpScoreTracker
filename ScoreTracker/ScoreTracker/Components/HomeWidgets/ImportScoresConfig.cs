using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Components.HomeWidgets;

// Import Scores widget config. Mix scope mirrors Quick Record (null Mix + AllMixes=false = follow
// the current mix). Remember-my-password is NOT here — the credential is per-device, stored via
// the configurator's Store form into local storage, never in this per-instance blob.
public sealed record ImportScoresConfig
{
    public MixEnum? Mix { get; set; }

    public bool AllMixes { get; set; }

    // On: use the saved game tag and import immediately. Off: pick a card after sign-in.
    public bool SkipGameTag { get; set; } = true;
}
