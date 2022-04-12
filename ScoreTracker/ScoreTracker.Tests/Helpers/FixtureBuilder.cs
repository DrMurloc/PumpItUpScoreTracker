using AutoFixture;
using ScoreTracker.Tests.AutoFixture;

namespace ScoreTracker.Tests.Helpers;

public static class FixtureBuilder
{
    public static Fixture Build()
    {
        var fixture = new Fixture();
        fixture.Customizations.Add(new DifficultyLevelBuilder());
        fixture.Customizations.Add(new NameBuilder());
        return fixture;
    }
}