using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts;

[ExcludeFromCodeCoverage]
public sealed record RevealedImportCredential(RedactedString Username, RedactedString Password);
