namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     The player can't be added as a rival. Deliberately says nothing about WHY beyond the plain
///     fact: a private player who never shared their code and a player who blocked you look
///     identical from here, and both should. Telling somebody they were blocked turns a quiet
///     control into a confrontation.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RivalNotAvailableException : Exception
{
    public RivalNotAvailableException() : base("That player isn't available to add as a rival.")
    {
    }
}
