using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Identity.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ExternalUserResolution(User User, bool IsNew)
{
}
