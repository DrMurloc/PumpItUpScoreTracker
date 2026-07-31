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
        typeof(UserDataPurge).GetMethod(nameof(DeleteFor), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo DeleteForNullableMethod =
        typeof(UserDataPurge).GetMethod(nameof(DeleteForNullable), BindingFlags.NonPublic | BindingFlags.Static)!;

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

    private static Task DeleteFor<TEntity>(ChartAttemptDbContext database, string column, Guid userId,
        CancellationToken cancellationToken) where TEntity : class
    {
        return database.Set<TEntity>().Where(e => EF.Property<Guid>(e, column) == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // A nullable owning key means the row is only sometimes a user's — a randomizer draw
    // belongs to a player or to a tournament, never both. NULL rows are nobody's and stay.
    private static Task DeleteForNullable<TEntity>(ChartAttemptDbContext database, string column, Guid userId,
        CancellationToken cancellationToken) where TEntity : class
    {
        return database.Set<TEntity>().Where(e => EF.Property<Guid?>(e, column) == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    ///     The Guid property naming the owning user, and the delete bound to its nullability.
    ///     Convention-resolved so a vertical's manifest stays a plain type list; a type carrying
    ///     two candidates throws rather than guessing which one means "whose data is this".
    /// </summary>
    private static (MethodInfo Bound, string Column) PlanFor(ChartAttemptDbContext database, Type entityType)
    {
        var mapped = database.Model.FindEntityType(entityType)
                     ?? throw new InvalidOperationException(
                         $"{entityType.Name} is not mapped on the shared context — a vertical declared it " +
                         "user-owned but never contributed it to the model.");

        var candidates = mapped.GetProperties()
            .Where(p => (p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?)) &&
                        p.Name.EndsWith("UserId", StringComparison.Ordinal))
            .ToArray();

        var key = candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"{entityType.Name} has no Guid *UserId property, so there is nothing to key a purge on."),
            _ => throw new InvalidOperationException(
                $"{entityType.Name} carries several user keys " +
                $"({string.Join(", ", candidates.Select(c => c.Name))}); purging it needs a hand-written " +
                "repository rather than the shared manifest.")
        };

        var method = key.ClrType == typeof(Guid?) ? DeleteForNullableMethod : DeleteForMethod;
        return (method.MakeGenericMethod(entityType), key.Name);
    }
}
