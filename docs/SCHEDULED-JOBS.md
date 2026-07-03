# Scheduled Jobs

Recurring background work is scheduled by **Hangfire** (SQL Server storage, `HangFire` schema — schedules survive restarts) and executed over **MassTransit**: each job is a one-line `IBus.Publish(...)` in [`RecurringJobRunner`](../ScoreTracker/ScoreTracker/HostedServices/RecurringJobRunner.cs), registered in [`Program.cs`](../ScoreTracker/ScoreTracker/Program.cs) via `RecurringJob.AddOrUpdate`. The published message is consumed by a saga inside the owning vertical.

All cron expressions are **UTC**.

| Job id | Cadence (UTC) | Message | Consumer (vertical) | What it does |
|---|---|---|---|---|
| `process-scores-tier-list` | Daily 07:00 | `ProcessScoresTiersListCommand` | TierListSaga (ChartIntelligence) | Rebuilds the score-based tier lists per level (1–29) and chart type: groups all player scores, weights them by competitive-level delta, filters statistical outliers, persists the rankings. |
| `calculate-scoring-difficulty` | Daily 08:00 | `RecalculateScoringDifficultyCommand` | ScoringDifficultySaga (ChartIntelligence) | Recalculates each chart's scoring-difficulty level from weighted scores in a ±3-level window, normalized against chart level and player depth. |
| `update-weekly-charts` | Daily 09:00 | `RotateWeeklyChartsCommand` | WeeklyTournamentSaga (WeeklyChallenge) | Rotates the weekly challenge board **if the current week has expired**: writes placement history, clears the board, draws fresh charts (excluding recently used ones), sets next Monday's expiration. Early-exits while the week is still active — the daily cadence is a retry envelope, not the rotation period. |
| `process-pass-tier-list` | Daily 09:30 | `ProcessPassTierListCommand` | TierListSaga (ChartIntelligence) | Rebuilds the pass-count and PG tier lists for levels 10–29 across Singles, Doubles, and CoOp, weighting each player's passes by their competitive threshold. |
| `calculate-chart-letter-difficulties` | Daily 10:00 | `RecalculateChartLetterDifficultiesCommand` | ScoringDifficultySaga (ChartIntelligence) | Recalculates per-chart letter-grade (AA–PG) difficulty percentiles from the share of players meeting each grade threshold, weighted by competitive level. |
| `start-leaderboard-import` | Sundays 10:30 | `StartLeaderboardImportCommand` | OfficialLeaderboardSaga (OfficialMirror) | Weekly sync against the official PiuGame site: imports chart leaderboards and rank lists, feeds the official-scores tier lists, recalculates world rankings, stamps the import state. |
| `try-schedule-mom` | Daily 11:00 | `TryScheduleMoMCommand` | MarchOfMurlocsHandler (EventCompetition) | Checks whether the current March of Murlocs tournament has expired; schedules the next cycle (delayed message) or cycles immediately. Idempotent — defers if a future MoM already exists. |
| `flush-overdue-score-batches` | Every 5 min | `FlushOverdueScoreBatchesCommand` | UpdatePhoenixRecordHandler (ScoreLedger) | Safety net for score-update batching: fires `PlayerScoresUpdatedEvent` for any accumulated score batch whose 2-minute hold window has expired. Compensates for the in-memory bus losing mid-flight work on restarts. |

## Operational notes

- **Dashboard**: `/hangfire`, gated on admin (`HangfireDashboardAuthorization`). Use it to inspect, trigger, or retry jobs.
- **`PreventRecurringJobs=true`** (config flag) removes all registrations instead of scheduling them — set in environments that shouldn't run background work.
- **Restart semantics**: schedules live in SQL and re-fire per Hangfire's misfire handling; but the MassTransit transport is **in-memory**, so a message that was mid-flight when the process died is gone. Jobs are designed to tolerate this (idempotent consumers, the flush job as a catch-up).
- **Adding a job** = one method on `RecurringJobRunner` + one `RecurringJob.AddOrUpdate` line in `Program.cs` + a consumer in the owning vertical. Do not introduce a second scheduler library or hosted-timer patterns.
