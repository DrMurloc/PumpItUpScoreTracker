namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     How a mirrored board tag is written out for people. Board tags are TAG#1234; the digits
///     identify an account rather than naming anyone, so every surface that lists players prints
///     the human half only and keeps the full tag somewhere it can still be read (a row tooltip
///     on the hub, the player's own profile header).
/// </summary>
/// <remarks>
///     This lives in Contracts rather than beside the one component that used to own it because
///     the Discord digest needs the same rule and cannot reference Web. Two copies of a display
///     rule is how the card ended up printing raw tags for a month after the hub stopped.
/// </remarks>
public static class OfficialPlayerNames
{
    /// <summary>The human half of a board tag — everything before the discriminator.</summary>
    public static string Human(string username)
    {
        var hash = username.IndexOf('#');
        return hash > 0 ? username[..hash] : username;
    }
}
