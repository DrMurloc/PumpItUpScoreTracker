using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Services;

public sealed class UiSettingsAccessor : IUiSettingsAccessor
{
    private const string MixKey = "Universal__CurrentMix";
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public UiSettingsAccessor(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public async Task<MixEnum> GetSelectedMix(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsLoggedIn) return MixEnum.XX;

        var settings = await _mediator.Send(new GetUserUiSettingsQuery(), cancellationToken);
        if (!settings.ContainsKey(MixKey)) return MixEnum.XX;
        return Enum.TryParse<MixEnum>(settings[MixKey], out var mix) ? mix : MixEnum.XX;
    }

    public async Task SetSelectedMix(MixEnum mix, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsLoggedIn) return;

        await _mediator.Send(new SaveUserUiSettingCommand(MixKey, mix.ToString()), cancellationToken);
    }
}