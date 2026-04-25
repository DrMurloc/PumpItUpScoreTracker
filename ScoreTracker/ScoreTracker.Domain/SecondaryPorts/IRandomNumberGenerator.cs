namespace ScoreTracker.Domain.SecondaryPorts;

public interface IRandomNumberGenerator
{
    int Next(int maxValue);
    double NextDouble();
}
