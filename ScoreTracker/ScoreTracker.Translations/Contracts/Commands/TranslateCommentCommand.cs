using MediatR;

namespace ScoreTracker.Translations.Contracts.Commands;

/// <summary>
///     Renders one piece of community text into every supported locale, whatever language it was
///     written in.
///     <para>
///         A command rather than a query: it spends metered tokens, so dispatching it is an act
///         with a cost, not a read.
///     </para>
///     <para>
///         <paramref name="PivotModelId" /> and <paramref name="FanOutModelId" /> are separate so
///         the two halves can run on different tiers — reading an arbitrary language and judging
///         its register is a harder job than rendering known English through a glossary, and the
///         probe exists to find out whether that difference is worth paying for.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TranslateCommentCommand(
    string Text,
    string PivotModelId,
    string FanOutModelId)
    : IRequest<TranslationOutcome>
{
    /// <summary>Both stages on one model — the arm the sweep runs three of.</summary>
    public TranslateCommentCommand(string text, string modelId) : this(text, modelId, modelId)
    {
    }
}
