using MediatR;

namespace ScoreTracker.Translations.Contracts.Commands;

/// <summary>
///     Re-queues every translated text — the move after a prompt or glossary change. Admin-only
///     by convention and admin-triggered only by design: the page quotes
///     <c>GetRetranslationCostEstimateQuery</c> before dispatching this, and nothing automatic
///     ever sends it. Existing renderings stay until the fresh ones arrive to replace them.
///     Returns how many texts were re-queued.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RetranslateAllCommand : IRequest<int>;
