using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Accessors;

public sealed class RandomNumberGenerator : IRandomNumberGenerator
{
    public int Next(int maxValue) => Random.Shared.Next(maxValue);
    public double NextDouble() => Random.Shared.NextDouble();
}
