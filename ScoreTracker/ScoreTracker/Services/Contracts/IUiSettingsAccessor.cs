using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.Contracts;

public interface IUiSettingsAccessor
{
    Task<MixEnum> GetSelectedMix(CancellationToken cancellationToken = default);
    Task<string?> GetSetting(string key, CancellationToken cancellationToken = default, Guid? userId = null);
    Task SetSetting(string key, string value, CancellationToken cancellationToken = default);
}