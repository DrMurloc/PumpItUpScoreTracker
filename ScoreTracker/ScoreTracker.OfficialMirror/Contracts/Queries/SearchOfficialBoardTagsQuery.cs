using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Board tags matching <paramref name="Term" /> that placed in the latest sealed snapshot,
///     capped at <paramref name="Take" />.
///     <para>
///         The term and the cap are part of the question because the caller is a per-keystroke
///         picker. Asking for the snapshot's whole tag population and filtering it in the handler
///         reads every board position in the snapshot to keep ten strings, and a picker that is
///         slow enough to still be running when the next character arrives gets cancelled — which
///         SqlClient reports as a severe command error, not as a cancellation.
///     </para>
///     <para>
///         Departed tags are excluded on purpose: offering one hands somebody a permanently empty
///         rivalry (docs/design/rivals.md D21).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SearchOfficialBoardTagsQuery(MixEnum Mix, string Term, int Take = 10)
    : IQuery<IReadOnlyList<string>>;
