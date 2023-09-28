using AutoFixture.Kernel;
using ScoreTracker.Domain.ValueTypes;
using System;

namespace ScoreTracker.Tests.AutoFixture;

public sealed class DifficultyLevelBuilder : ISpecimenBuilder
{
    private readonly Random _random = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (request is not Type type || type != typeof(DifficultyLevel))
            return new NoSpecimen();

        var level = _random.Next(DifficultyLevel.Min, DifficultyLevel.Max);
        return DifficultyLevel.From(level);
    }
}