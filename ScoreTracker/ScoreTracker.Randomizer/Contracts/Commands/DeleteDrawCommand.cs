using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>Removes a match draw (mislabels happen). Staff-only; cards cascade.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record DeleteDrawCommand(Guid DrawId) : IRequest
    {
    }
}
