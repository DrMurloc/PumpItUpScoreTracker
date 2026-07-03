using System;
using System.Linq;
using System.Reflection;
using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Credential-hygiene ratchet (login-overhaul design, 2026-07-03): message records carrying
///     a password must type it as RedactedString, never string. C# records auto-generate
///     ToString over all members, so a raw string password leaks the moment anyone logs the
///     request object, interpolates it, or embeds it in an exception message.
/// </summary>
public sealed class CredentialHygieneTests
{
    private static readonly Assembly[] MessageAssemblies =
    {
        typeof(Application.Commands.UpdateMatchCommand).Assembly,
        typeof(PlayerProgress.Contracts.Queries.GetTop50CompetitiveQuery).Assembly,
        typeof(Domain.Models.User).Assembly,
        typeof(Ucs.Contracts.UcsChart).Assembly,
        typeof(ScoreLedger.Contracts.Queries.GetPhoenixRecordQuery).Assembly,
        typeof(OfficialMirror.Contracts.Queries.GetGameCardsQuery).Assembly,
        typeof(Catalog.Contracts.Queries.GetRandomSettingsQuery).Assembly,
        typeof(ChartIntelligence.Contracts.Messages.ProcessPassTierListCommand).Assembly,
        typeof(WeeklyChallenge.Contracts.Messages.RotateWeeklyChartsCommand).Assembly,
        typeof(EventCompetition.Contracts.Messages.TryScheduleMoMCommand).Assembly,
        typeof(Communities.Contracts.Commands.CreateCommunityCommand).Assembly,
        typeof(Identity.Contracts.Commands.CreateUserCommand).Assembly
    };

    [Fact]
    public void PasswordPropertiesOnMessageRecordsAreRedactedStrings()
    {
        var violations = MessageAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsInterface && !t.Name.Contains('<'))
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t)
                        || (t.Namespace != null &&
                            (t.Namespace.Contains(".Contracts", StringComparison.Ordinal) ||
                             t.Namespace.EndsWith(".Messages", StringComparison.Ordinal))))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                            && p.PropertyType != typeof(RedactedString))
                .Select(p => $"{t.FullName}.{p.Name}"))
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Password-bearing message properties must be RedactedString, not string: {string.Join(", ", violations)}");
    }
}
