namespace ScoreTracker.Domain.Records;

/// <summary>A Discord account as the bot sees it.</summary>
/// <param name="Username">The account's unique handle, the one a person types to find it.</param>
/// <param name="GlobalName">The display name, when the account set one.</param>
[ExcludeFromCodeCoverage]
public sealed record BotUser(ulong Id, string Username, string? GlobalName);
