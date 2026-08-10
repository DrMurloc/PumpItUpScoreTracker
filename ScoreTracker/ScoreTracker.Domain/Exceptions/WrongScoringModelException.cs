using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Exceptions;

/// <summary>
///     A score submitted in the wrong scoring model for its mix. Phoenix-generation mixes
///     record a 1,000,000-scale score with a plate; every older mix records a letter grade
///     with an optional era-scale number, and the two live in different stores. A record
///     written through the wrong path is not merely mislabelled — it lands somewhere no
///     read path for that mix will ever look.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class WrongScoringModelException : Exception
{
    public WrongScoringModelException(MixEnum mix, string expected) : base(
        $"{mix.GetName()} records a {expected}.")
    {
        Mix = mix;
    }

    public MixEnum Mix { get; }
}
