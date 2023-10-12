using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Web.Services.Contracts;

public interface IUiSettingsAccessor
{
    Task<MixEnum> GetSelectedMix(CancellationToken cancellationToken = default);
    Task SetSelectedMix(MixEnum mix, CancellationToken cancellationToken = default);

    Task<string?> GetSetting(string key, CancellationToken cancellationToken = default);
    Task SetSetting(string key, string value, CancellationToken cancellationToken = default);
}