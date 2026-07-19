using ScoreTracker.WeeklyChallenge.Contracts;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Web.Components.Challenges;

/// <summary>One pre-rendered slot of the monthly rail card: a type and its board.</summary>
public sealed record MonthlyRailBoard(ChartType? Type, MonthlyLeaderboardView View);
