---
prefix: WEEKLY
claims:
  routes: [/WeeklyCharts]
  jobs: [update-weekly-charts]
---
# Weekly charts - the charts everyone plays this week

## Purpose

Every mix with a catalog has a weekly board: a handful of charts drawn for the week, a leaderboard per chart, and
placements that are kept when the week ends. Players see the board on `/WeeklyCharts`; anonymous visitors see the
same board and leaderboards.

## Requirements

### Requirement: WEEKLY-001 The board rotates when its week ends
Each mix's weekly board MUST be rotated by the first daily run of `update-weekly-charts` after its week expires,
at 05:00 UTC on a Monday.

#### Scenario: the Monday rotation
- **GIVEN** the Phoenix 2 board's week expires at 05:00 UTC on 2026-11-23
- **WHEN** `update-weekly-charts` runs at 05:00 UTC on 2026-11-23
- **THEN** the finishing placements are written to the placement history
- **AND** fresh charts are drawn, leaving out recently used ones
- **AND** the board's next expiration is 05:00 UTC on 2026-11-30

#### Scenario: a run inside the week changes nothing
- **GIVEN** the Phoenix 2 board's week expires at 05:00 UTC on 2026-11-30
- **WHEN** `update-weekly-charts` runs at 05:00 UTC on 2026-11-25
- **THEN** the board keeps its charts and its entries

### Requirement: WEEKLY-002 Which entries count
An entry MUST count on the weekly board according to where it came from:

| Source | Counts |
|---|---|
| An official import | Yes |
| A score typed by the player | Yes, marked as unverified |
| A score posted by a partner tool | Yes, marked as unverified |

#### Scenario: a typed score is marked
- **GIVEN** the player PlayerOne types a score of 987,654 on a chart on this week's board
- **WHEN** a visitor opens `/WeeklyCharts`
- **THEN** PlayerOne's entry shows 987,654 marked as unverified
