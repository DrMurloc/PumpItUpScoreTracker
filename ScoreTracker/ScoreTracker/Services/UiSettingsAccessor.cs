using MediatR;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
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
    private readonly ProtectedLocalStorage _browserStorage;

    public UiSettingsAccessor(IMediator mediator, ICurrentUserAccessor currentUser,
        ProtectedLocalStorage browserStorage)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _browserStorage = browserStorage;
    }

    public async Task<MixEnum> GetSelectedMix(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsLoggedIn)
        {
            var browserValue = await _browserStorage.GetAsync<string>(MixKey);
            if (browserValue.Success && Enum.TryParse<MixEnum>(browserValue.Value, out var browserMix))
                return browserMix;
            return MixEnum.Phoenix;
        }

        var settings = await _mediator.Send(new GetUserUiSettingsQuery(), cancellationToken);
        if (!settings.ContainsKey(MixKey)) return MixEnum.Phoenix;
        return Enum.TryParse<MixEnum>(settings[MixKey], out var mix) ? mix : MixEnum.Phoenix;
    }

    public async Task SetSelectedMix(MixEnum mix, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsLoggedIn)
        {
            await _browserStorage.SetAsync(MixKey, mix.ToString());
            return;
        }

        await _mediator.Send(new SaveUserUiSettingCommand(MixKey, mix.ToString()), cancellationToken);
    }

    public async Task<string?> GetSetting(string key, CancellationToken cancellationToken = default,
        Guid? userId = null)
    {
        if (_currentUser.IsLoggedIn)
        {
            var setting = await _mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken);
            return setting.TryGetValue(key, out var value) ? value : null;
        }

        var browserValue = await _browserStorage.GetAsync<string>(key);
        return browserValue.Success ? browserValue.Value : null;
    }

    public async Task SetSetting(string key, string value, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsLoggedIn)
        {
            await _mediator.Send(new SaveUserUiSettingCommand(key, value), cancellationToken);
            return;
        }

        await _browserStorage.SetAsync(key, value);
    }
}