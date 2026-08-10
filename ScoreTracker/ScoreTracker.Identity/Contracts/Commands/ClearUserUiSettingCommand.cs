using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

/// <summary>
///     Removes a setting rather than writing a value to it. "Absent" is a real answer for some
///     preferences — the language picker's Automatic means "follow the browser", which is exactly
///     the state of a player who never chose (docs/design/culture-resolution.md).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ClearUserUiSettingCommand(string SettingName) : IRequest
{
}
