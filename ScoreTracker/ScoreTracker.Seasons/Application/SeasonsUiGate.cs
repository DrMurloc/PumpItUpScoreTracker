using Microsoft.Extensions.Options;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Seasons.Contracts;
using ScoreTracker.Seasons.Wiring;

namespace ScoreTracker.Seasons.Application;

/// <summary>
///     The flag, or the admin. One place, so a surface that forgets the admin bypass or reads the
///     flag directly has nowhere to do it.
/// </summary>
internal sealed class SeasonsUiGate : ISeasonsUiGate
{
    private readonly IOptions<SeasonsConfiguration> _configuration;
    private readonly ICurrentUserAccessor _currentUser;

    public SeasonsUiGate(IOptions<SeasonsConfiguration> configuration, ICurrentUserAccessor currentUser)
    {
        _configuration = configuration;
        _currentUser = currentUser;
    }

    public bool IsOpen => _configuration.Value.EnableUI || _currentUser.IsLoggedInAsAdmin;
}
