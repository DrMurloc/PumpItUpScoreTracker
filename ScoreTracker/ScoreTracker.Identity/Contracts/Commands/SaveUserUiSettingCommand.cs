using MediatR;

namespace ScoreTracker.Identity.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record SaveUserUiSettingCommand(string SettingName, string NewValue) : IRequest
{
}
