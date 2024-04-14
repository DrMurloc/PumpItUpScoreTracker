using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IQualifiersRepository
    {
        Task<UserQualifiers?> GetQualifiers(Guid tournamentId, Name userName, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task SaveQualifiers(Guid tournamentId, UserQualifiers qualifiers,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<UserQualifiers>> GetAllUserQualifiers(Guid tournamentId, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task<QualifiersConfiguration> GetQualifiersConfiguration(Guid tournamentId,
            CancellationToken cancellationToken = default);
    }
}
