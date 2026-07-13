namespace ScoreTracker.Web.Enums
{
    // The draft-mode tap action (docs/design/randomizer-overhaul.md): in compact
    // density the armed action is what tapping a draw card does; tapping the card
    // again undoes it. Details live on the comfortable/table views, not here.
    public enum ArmedAction
    {
        Protect,
        Veto
    }
}
