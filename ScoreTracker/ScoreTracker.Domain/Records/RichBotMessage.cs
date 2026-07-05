namespace ScoreTracker.Domain.Records;

/// <summary>
///     Provider-agnostic rich bot message: one card. Sagas compose these; the Discord
///     adapter renders them to Components V2 (or a flattened plain-text fallback).
///     Every string field carries the same emoji-token vocabulary as plain messages
///     (#LETTERGRADE|…#, #PLATE|…#, #DIFFICULTY|…#, #MIX|…#) — the adapter owns the swap.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RichBotMessage(
    RichBotSection? Header,
    IReadOnlyList<IRichBotBlock> Blocks,
    string? Footer,
    uint? AccentColor,
    IReadOnlyList<RichBotLink> Links);

/// <summary>Closed set: <see cref="RichBotText" />, <see cref="RichBotSection" />, <see cref="RichBotDivider" />.</summary>
public interface IRichBotBlock
{
}

[ExcludeFromCodeCoverage]
public sealed record RichBotText(string Markdown) : IRichBotBlock;

[ExcludeFromCodeCoverage]
public sealed record RichBotSection(string Markdown, Uri? Thumbnail) : IRichBotBlock;

[ExcludeFromCodeCoverage]
public sealed record RichBotDivider : IRichBotBlock;

[ExcludeFromCodeCoverage]
public sealed record RichBotLink(string Label, Uri Url);
