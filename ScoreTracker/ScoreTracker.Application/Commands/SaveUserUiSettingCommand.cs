using MediatR;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record SaveUserUiSettingCommand(string SettingName, string NewValue) : IRequest
{
}
