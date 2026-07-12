using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>Renames a match draw; the slug (and spectate links) never change. Head TO/TO only.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record RenameDrawCommand(Guid DrawId, string NewName) : IRequest
    {
    }
}
