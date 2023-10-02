using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IQualifiersRepository
    {
        Task<UserQualifiers?> GetQualifiers(Name userName, QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task SaveQualifiers(UserQualifiers qualifiers, CancellationToken cancellationToken = default);

        Task<IEnumerable<UserQualifiers>> GetAllUserQualifiers(QualifiersConfiguration config,
            CancellationToken cancellationToken = default);

        Task<QualifiersConfiguration> GetQualifiersConfiguration(
            CancellationToken cancellationToken = default);
    }
}
