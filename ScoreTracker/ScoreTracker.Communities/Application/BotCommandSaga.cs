using MediatR;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Application
{
    /// <summary>
    ///     Routes every /piu slash-command invocation and autocomplete lookup. Feature-grouped
    ///     like the other sagas: one class owns the whole command surface, dispatching to the
    ///     other verticals through their published contracts. Composition lives here (not in the
    ///     bot host) so every reply is testable in the fast component suite.
    /// </summary>
    internal sealed class BotCommandSaga :
        IRequestHandler<HandleBotInteractionCommand, BotReply>,
        IRequestHandler<GetBotAutocompleteQuery, IReadOnlyList<BotOptionChoice>>
    {
        public Task<BotReply> Handle(HandleBotInteractionCommand request, CancellationToken cancellationToken)
        {
            var interaction = request.Interaction;
            var command = interaction.CommandPath.Count > 0 ? interaction.CommandPath[0] : string.Empty;
            return command switch
            {
                "calc" => Task.FromResult(Calc(interaction)),
                _ => Task.FromResult(new BotReply(Text: "That command isn't available yet."))
            };
        }

        public Task<IReadOnlyList<BotOptionChoice>> Handle(GetBotAutocompleteQuery request,
            CancellationToken cancellationToken)
        {
            // No autocompleting options exist yet; later commits add community, feed, and
            // preset lookups keyed on the focused option name.
            return Task.FromResult<IReadOnlyList<BotOptionChoice>>(Array.Empty<BotOptionChoice>());
        }

        private static BotReply Calc(BotInteraction interaction)
        {
            var perfects = ReadInt(interaction, "perfects");
            var greats = ReadInt(interaction, "greats");
            var goods = ReadInt(interaction, "goods");
            var bads = ReadInt(interaction, "bads");
            var misses = ReadInt(interaction, "misses");
            var combo = ReadInt(interaction, "combo");
            double? calories = interaction.Options.TryGetValue("calories", out var calorieString) &&
                               double.TryParse(calorieString, out var parsed)
                ? parsed
                : null;

            var screen = new ScoreScreen(perfects, greats, goods, bads, misses, combo, calories);
            if (!screen.IsValid)
                return new BotReply(Text: "That scoring configuration is invalid.");

            var loss = (double)(1000000 - screen.CalculatePhoenixScore);
            var message = $@"{perfects:N0} Perfects, {greats:N0} Greats, {goods:N0} Goods, {bads:N0} Bads, {misses:N0} Misses, {combo:N0} Max Combo
**{(int)screen.CalculatePhoenixScore:N0} (#LETTERGRADE|{screen.LetterGrade}##PLATE|{screen.PlateText}#)**
{screen.NextLetterGrade()}
- {screen.GreatLoss:N0} Lost to Greats ({SafePercent(screen.GreatLoss, loss)}%)
- {screen.GoodLoss:N0} Lost to Goods ({SafePercent(screen.GoodLoss, loss)}%)
- {screen.BadLoss:N0} Lost to Bads ({SafePercent(screen.BadLoss, loss)}%)
- {screen.MissLoss:N0} Lost to Misses ({SafePercent(screen.MissLoss, loss)}%)
- {screen.ComboLoss:N0} Lost to Combo ({SafePercent(screen.ComboLoss, loss)}%)";
            if (screen.EstimatedSteps != null)
                message += $"{Environment.NewLine}- {screen.EstimatedSteps:N0} Estimated Arrow Presses";

            return new BotReply(Text: message);
        }

        private static string SafePercent(int part, double whole) =>
            whole <= 0 ? "0.00" : (100.0 * part / whole).ToString("N2");

        private static int ReadInt(BotInteraction interaction, string key) =>
            interaction.Options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
