namespace ScoreTracker.Web.Enums
{
    // The draft-mode tap action (docs/design/randomizer-overhaul.md): in compact
    // density the armed action is what tapping a draw card does; tapping the card
    // again undoes it. Details is the browse mode between rounds.
    public enum ArmedAction
    {
        Protect,
        Veto,
        Details
    }
}
