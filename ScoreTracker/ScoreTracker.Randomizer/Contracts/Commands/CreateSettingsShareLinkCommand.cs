using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Mints (or returns the existing) share token for one of the current user's saved
    ///     settings. Anyone with the link can preview and import a copy into their own
    ///     library — settings sharing needs no tournament (docs/design/randomizer-overhaul.md).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record CreateSettingsShareLinkCommand(Name SettingsName) : IRequest<Guid>
    {
    }
}
