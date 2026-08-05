namespace ScoreTracker.Domain.Records;

/// <summary>
///     One completion request to a large language model. <paramref name="ModelId" /> is opaque
///     here on purpose — the Domain does not know which provider is behind the port, only that
///     callers pick a model and the adapter understands the string.
///     <para>
///         <paramref name="JsonSchema" />, when set, constrains the response to that shape so the
///         caller parses rather than salvages. A null schema means free-form prose.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LanguageModelRequest(
    string ModelId,
    string SystemPrompt,
    string UserPrompt,
    string? JsonSchema = null);

/// <summary>
///     What one call consumed. The two cache fields are separate from
///     <paramref name="InputTokens" /> because they bill at different rates — a read is a
///     fraction of the input price and a write is a premium over it — so a caller measuring
///     cost needs all four, not a total.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LanguageModelUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens = 0,
    int CacheReadInputTokens = 0);

/// <summary>
///     A completed call. <paramref name="ModelId" /> is what actually served the response, which
///     is not always what was asked for.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LanguageModelResponse(string Text, string ModelId, LanguageModelUsage Usage);
