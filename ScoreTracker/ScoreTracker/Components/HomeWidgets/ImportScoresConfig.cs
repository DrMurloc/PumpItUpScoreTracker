using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;

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

    /// <summary>
    ///     Superseded and no longer read. The choice moved to the account
    ///     (<see cref="BrokenScorePreference" />) because a player means the same thing whichever
    ///     surface they import from, and two widgets could otherwise hold opposite answers.
    ///     Kept so an instance saved before the move still deserializes.
    ///     <para>
    ///         Retired outright rather than migrated because there was nothing to migrate: of the
    ///         311 <c>import-scores</c> widgets in production on 2026-08-11, zero carried this key
    ///         in their config blob. Nobody ever moved it off "follow the mix".
    ///     </para>
    /// </summary>
    public bool? RecordBrokenAsBest { get; set; }
}
