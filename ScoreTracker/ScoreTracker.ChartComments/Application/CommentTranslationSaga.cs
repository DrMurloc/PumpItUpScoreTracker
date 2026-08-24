using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Translations.Contracts.Events;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     Lands a finished translation on its comment. This is the authoritative end of the link
///     defence: the pipeline verified markers, but the substitution back to real URLs happens
///     here, and a substituted rendering whose link set differs from the comment's — judged by
///     the same parser that autolinks at render — is never written. The failure mode everywhere
///     is "that locale keeps the original", never a broken comment.
/// </summary>
internal sealed class CommentTranslationSaga : IConsumer<TextTranslatedEvent>
{
    private readonly ICommentRepository _comments;
    private readonly ICommentRenderingRepository _renderings;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<CommentTranslationSaga> _logger;

    public CommentTranslationSaga(ICommentRepository comments, ICommentRenderingRepository renderings,
        IDateTimeOffsetAccessor clock, ILogger<CommentTranslationSaga> logger)
    {
        _comments = comments;
        _renderings = renderings;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TextTranslatedEvent> context)
    {
        // The pipeline serves any text owner; keys that are not ours are not ours to act on.
        var commentId = CommentSourceKeys.TryParse(context.Message.SourceKey);
        if (commentId == null) return;

        var comment = await _comments.GetById(commentId.Value, context.CancellationToken);
        // Gone, taken down, or a note that should never have been queued — nothing to decorate.
        if (comment == null || comment.IsDeleted || comment.Audience.IsPrivate) return;

        // An edit that landed while the batch flew re-marked the text; these renderings describe
        // words that no longer exist. The edit's own path already decided whether to re-queue.
        var marked = CommentText.ExtractLinks(comment.Text);
        if (!string.Equals(marked.Text, context.Message.SourceText, StringComparison.Ordinal)) return;

        var kept = new Dictionary<string, string>();
        foreach (var (locale, rendered) in context.Message.Translations)
        {
            var substituted = marked.Substitute(rendered);
            if (!CommentText.LinkSetsMatch(comment.Text, substituted))
            {
                _logger.LogWarning(
                    "Discarding the {Locale} rendering of comment {CommentId}: its links differ from the source",
                    locale, commentId);
                continue;
            }

            kept[locale] = substituted;
        }

        if (kept.Count == 0) return;

        await _renderings.StoreTranslation(comment.Id, context.Message.SourceLanguage, kept,
            context.Message.TranslatedBy, _clock.Now, context.CancellationToken);
    }
}
