namespace ScoreTracker.Data.Persistence;

/// <summary>
///     Names the property that says <em>whose</em> a row is, for an entity carrying more than one
///     Guid *UserId column. <see cref="UserDataPurge" /> resolves the owning key by convention and
///     refuses to guess between candidates, which is the right default — the other key on such a
///     row usually points at a different person entirely, and deleting on it takes their data
///     instead. This is how an entity resolves that itself, next to the columns rather than in a
///     purge repository that would have to be kept in step by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PurgeKeyAttribute : Attribute
{
    public PurgeKeyAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
