using ScoreTracker.PlayerProgress.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>
///     The Energy control's copy (docs/design/pumbility-overhaul.md D51): the options in the order
///     the select offers them, each option's label key and its hint key.
///     <para>
///         The copy is Good, Great and Top of my game — exactly the approved mock. The KEYS for the
///         first two are not the words themselves: "Good" and "Great" are the judgement names' resx
///         keys, and a key reused across two meanings hands a Korean or Japanese reader the
///         judgement's translation for an energy level. So the key carries the register
///         ("Energy: Good") and the English value stays the word. A collision in the resx is solved
///         in the resx; it never changes what the player reads.
///     </para>
/// </summary>
public static class EnergyLabels
{
    /// <summary>The options in the order the select offers them: the safe read first, the default.</summary>
    public static readonly Energy[] Options = { Energy.Good, Energy.Great, Energy.TopOfMyGame };

    /// <summary>The option's label, as a localization key.</summary>
    public static string Label(Energy energy)
    {
        return energy switch
        {
            Energy.Great => "Energy: Great",
            Energy.TopOfMyGame => "Top of my game",
            _ => "Energy: Good"
        };
    }

    /// <summary>What the option's percentile means, as a localization key — the item's tooltip.</summary>
    public static string Hint(Energy energy)
    {
        return energy switch
        {
            Energy.Great => "The middle of your peers.",
            Energy.TopOfMyGame => "A score only one in four of your peers beat.",
            _ => "A score three in four of your peers reach."
        };
    }
}
