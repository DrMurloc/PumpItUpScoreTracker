using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Contracts.Queries
{
    /// <summary>
    ///     A live autocomplete lookup for a /piu option, dispatched by the bot host as the
    ///     user types. Returns up to 25 choices for the focused option.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetBotAutocompleteQuery(BotAutocompleteRequest Request)
        : IQuery<IReadOnlyList<BotOptionChoice>>
    {
    }
}
