namespace ScoreTracker.Tests.Integration.Fixtures;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Integration";
}
