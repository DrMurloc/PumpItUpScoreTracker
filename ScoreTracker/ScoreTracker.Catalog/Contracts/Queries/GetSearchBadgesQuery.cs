using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The granular badge vocabulary for the SRP skills facet — every distinct piucenter
///     top-3 badge in the banked data, display-named. The cloud grows when piucenter's
///     vocabulary does; no code change.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSearchBadgesQuery : IQuery<IReadOnlyList<ChartBadge>>;
