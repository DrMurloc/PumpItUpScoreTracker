using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Translations.Contracts;
using ScoreTracker.Translations.Contracts.Commands;
using ScoreTracker.Translations.Domain;

namespace ScoreTracker.Translations.Application;

/// <summary>
///     Runs the two stages: the comment into English and its register, then that into every
///     target locale.
///     <para>
///         The pivot runs even when the comment is already English. It costs a fraction of the
///         fan-out and it is what produces the register metadata, without which the four
///         renderings would be reading tone out of bare prose. An English comment simply comes
///         back out of stage one unchanged.
///     </para>
///     <para>
///         Every call's usage is returned rather than logged, because the first caller is a cost
///         probe and a total it cannot attribute to a stage answers nothing.
///     </para>
/// </summary>
internal sealed class TranslateCommentHandler(ILanguageModelClient languageModel)
    : IRequestHandler<TranslateCommentCommand, TranslationOutcome>
{
    public async Task<TranslationOutcome> Handle(TranslateCommentCommand request,
        CancellationToken cancellationToken)
    {
        var pivotResponse = await languageModel.Complete(new LanguageModelRequest(
            request.PivotModelId,
            PivotPrompt.System(),
            PivotPrompt.User(request.Text),
            PivotPrompt.Schema), cancellationToken);

        var pivot = TranslationResponseReader.ReadPivot(pivotResponse.Text);

        // Never render a comment back into the language it arrived in. That round trip is not a
        // no-op, it is a rewrite: it raises casual endings into polite ones, swaps community
        // vocabulary for neutral words, converts dialect the reader could have read as written,
        // and occasionally corrupts a number. The original is already there and already better.
        var targets = TranslationTarget.ForSource(pivot.SourceLanguage);

        var fanOutResponse = await languageModel.Complete(new LanguageModelRequest(
            request.FanOutModelId,
            FanOutPrompt.System(targets),
            FanOutPrompt.User(PivotPrompt.Render(pivot)),
            FanOutPrompt.Schema(targets)), cancellationToken);

        var translations = TranslationResponseReader.ReadTranslations(fanOutResponse.Text);

        return new TranslationOutcome(pivot, translations, new[]
        {
            new TranslationCall("pivot", pivotResponse.ModelId, pivotResponse.Usage),
            new TranslationCall("fan-out", fanOutResponse.ModelId, fanOutResponse.Usage)
        });
    }
}
