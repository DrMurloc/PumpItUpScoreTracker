using MediatR;

namespace ScoreTracker.Application.Commands;

public sealed record SaveUserUiSettingCommand(string SettingName, string NewValue) : IRequest
{
}