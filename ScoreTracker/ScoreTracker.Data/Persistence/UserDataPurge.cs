using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence;

/// <summary>
///     Deletes a user's rows from a declared set of entity types. Each vertical's
///     AccountPurgeRepository states the types it owns and hands them here, so the declaration
///     and the deletion are the same list — a table added to one is added to the other, and
///     AccountPurgeCoverageTests checks that list against every user-keyed entity in the
///     assembly. The alternative, hand-written deletes beside a hand-written manifest, drifts
///     the moment someone updates one and not the other.
/// </summary>
public static class UserDataPurge
{
    private static readonly MethodInfo DeleteForMethod =
        typeof(UserDataPurge).GetMethod(nameof(DeleteFor), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo DeleteForNullableMethod =
        typeof(UserDataPurge).GetMethod(nameof(DeleteForNullable), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, (MethodInfo Bound, string Column)> Plans = new();

    /// <summary>
    ///     Deletes every row keyed to <paramref name="userId" /> across <paramref name="userOwned" />,
    ///     in the order given — which is how a vertical expresses FK ordering between its own tables.
    /// </summary>
    public static async Task DeleteAll(IDbContextFactory<ChartAttemptDbContext> factory,
        IReadOnlyList<Type> userOwned, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        foreach (var entityType in userOwned)
        {
            var (bound, column) = Plans.GetOrAdd(entityType, t => PlanFor(database, t));
            await (Task)bound.Invoke(null, new object[] { database, column, userId, cancellationToken })!;
        }
    }

    /// <summary>
    ///     Public only so the reflection above binds without asking for non-public members —
    ///     an accessibility bypass in a helper whose whole job is deleting rows is worth not
    ///     having. Call it through <see cref="DeleteAll" />.
    /// </summary>
    public static Task DeleteFor<TEntity>(ChartAttemptDbContext database, string column, Guid userId,
        CancellationToken cancellationToken) where TEntity : class
    {
        return database.Set<TEntity>().Where(e => EF.Property<Guid>(e, column) == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // A nullable owning key means the row is only sometimes a user's — a randomizer draw
    // belongs to a player or to a tournament, never both. NULL rows are nobody's and stay.
    // Public for the same reason as DeleteFor above.
    public static Task DeleteForNullable<TEntity>(ChartAttemptDbContext database, string column, Guid userId,
        CancellationToken cancellationToken) where TEntity : class
    {
        return database.Set<TEntity>().Where(e => EF.Property<Guid?>(e, column) == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    ///     The Guid property naming the owning user, and the delete bound to its nullability.
    ///     Convention-resolved so a vertical's manifest stays a plain type list; a type carrying
    ///     two candidates throws rather than guessing which one means "whose data is this", unless
    ///     it settles the question itself with <see cref="PurgeKeyAttribute" />.
    /// </summary>
    private static (MethodInfo Bound, string Column) PlanFor(ChartAttemptDbContext database, Type entityType)
    {
        var mapped = database.Model.FindEntityType(entityType)
                     ?? throw new InvalidOperationException(
                         $"{entityType.Name} is not mapped on the shared context — a vertical declared it " +
                         "user-owned but never contributed it to the model.");

        var guids = mapped.GetProperties()
            .Where(p => p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?))
            .ToArray();
        var declared = entityType.GetCustomAttribute<PurgeKeyAttribute>()?.PropertyName;
        var candidates = guids.Where(p => p.Name.EndsWith("UserId", StringComparison.Ordinal)).ToArray();

        // A declared key is looked up among every mapped Guid, not just the *UserId ones, so an
        // entity naming its owner column something else can still be purged by declaring it.
        var key = declared is not null
            ? guids.FirstOrDefault(p => p.Name == declared)
              ?? throw new InvalidOperationException(
                  $"{entityType.Name} declares [PurgeKey(\"{declared}\")] but has no mapped Guid property " +
                  "by that name, so the purge has nothing to key on. The name is the property's, not the " +
                  "column's.")
            : candidates.Length switch
            {
                1 => candidates[0],
                0 => throw new InvalidOperationException(
                    $"{entityType.Name} has no Guid *UserId property, so there is nothing to key a purge on."),
                _ => throw new InvalidOperationException(
                    $"{entityType.Name} carries several user keys " +
                    $"({string.Join(", ", candidates.Select(c => c.Name))}) and does not say which one owns " +
                    "the row. Mark it [PurgeKey(nameof(...))] with the key whose user the row belongs to — " +
                    "the others point at different people, and deleting on one of those takes their data.")
            };

        var method = key.ClrType == typeof(Guid?) ? DeleteForNullableMethod : DeleteForMethod;
        return (method.MakeGenericMethod(entityType), key.Name);
    }
}
