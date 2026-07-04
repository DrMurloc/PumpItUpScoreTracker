namespace ScoreTracker.Tests.E2E.Support;

/// <summary>
///     Every E2E test class shares the one app/SQL/WireMock/browser stack and runs
///     serially — each class resets and reseeds the database in InitializeAsync.
/// </summary>
[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<E2EAppFixture>
{
}
