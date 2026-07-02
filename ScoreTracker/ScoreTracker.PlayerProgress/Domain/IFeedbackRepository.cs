using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     Recommendation feedback storage. Vertical-internal: RecommendedChartsSaga is the
///     only reader/writer — this used to squat on IUserRepository as Get/SaveFeedback.
/// </summary>
internal interface IFeedbackRepository
{
    Task SaveFeedback(Guid userId, SuggestionFeedbackRecord feedback, CancellationToken cancellationToken = default);

    Task<IEnumerable<SuggestionFeedbackRecord>> GetFeedback(Guid userId,
        CancellationToken cancellationToken = default);
}
