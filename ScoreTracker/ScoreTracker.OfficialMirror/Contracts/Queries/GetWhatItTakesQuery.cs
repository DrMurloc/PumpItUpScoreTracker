using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     The "What It Takes" tab: the selected PUMBILITY board's tier ladder with grade-level
///     equivalents (50 uniform charts, SG plates assumed), the three-board entry
///     comparison, and the entry bar's history. Type: "All" / "Singles" / "Doubles".
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetWhatItTakesQuery(MixEnum Mix, string Type = "All") : IQuery<WhatItTakesRecord>;
