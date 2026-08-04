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

        var fanOutResponse = await languageModel.Complete(new LanguageModelRequest(
            request.FanOutModelId,
            FanOutPrompt.System(),
            FanOutPrompt.User(PivotPrompt.Render(pivot)),
            FanOutPrompt.Schema), cancellationToken);

        var translations = TranslationResponseReader.ReadTranslations(fanOutResponse.Text);

        return new TranslationOutcome(pivot, translations, new[]
        {
            new TranslationCall("pivot", pivotResponse.ModelId, pivotResponse.Usage),
            new TranslationCall("fan-out", fanOutResponse.ModelId, fanOutResponse.Usage)
        });
    }
}
