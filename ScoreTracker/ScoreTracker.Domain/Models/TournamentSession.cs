using ScoreTracker.SharedKernel.Models;
﻿using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class TournamentSession
    {
        // Verification is gone (D5, march-of-murlocs.md): sessions publish without approval,
        // and a video survives purely as a showcase link.
        public Uri? VideoUrl { get; set; }

        public Guid UsersId { get; }
        public Guid TournamentId => _configuration.Id;
        private readonly TournamentConfiguration _configuration;
        public IList<Entry> Entries { get; }

        public int CurrentScore { get; }

        // Mutable: stamina session registration picks the mix (Phoenix/Phoenix2) before any
        // entries exist; persistence round-trips it through UserTournamentSessionEntity.MixId.
        public MixEnum Mix { get; set; }

        public TournamentSession(Guid userId, TournamentConfiguration configuration, MixEnum mix = MixEnum.Phoenix)
        {
            _configuration = configuration;
            Entries = new List<Entry>();
            CurrentScore = 0;
            UsersId = userId;
            Mix = mix;
        }

        public TournamentSession(Guid userId, TournamentConfiguration configuration, IEnumerable<Entry> entries,
            MixEnum mix = MixEnum.Phoenix)
        {
            _configuration = configuration;
            Entries = entries.ToList();
            CurrentScore = Entries.Sum(e => e.SessionScore);
            UsersId = userId;
            Mix = mix;
        }

        // A session whose closing chart overhangs the window has no rest by construction —
        // it filled the window — so derived rest floors at zero rather than going negative
        // (it persists to storage that rejects a negative).
        public TimeSpan CurrentRestTime =>
            TotalPlayTime >= _configuration.MaxTime ? TimeSpan.Zero : _configuration.MaxTime - TotalPlayTime;

        public TimeSpan AverageTimeBetweenCharts =>
            Entries.Count <= 1 ? _configuration.MaxTime : CurrentRestTime / Entries.Count;

        public TimeSpan AverageTimeWithAddedChart(Chart chart)
        {
            var charts = Entries.Select(e => e.Chart).Append(chart).ToArray();
            var totalPlayTime = TimeSpan.FromTicks(charts.Sum(c => c.Song.Duration.Ticks));
            var restTime = totalPlayTime >= _configuration.MaxTime
                ? TimeSpan.Zero
                : _configuration.MaxTime - totalPlayTime;

            return charts.Length <= 1 ? _configuration.MaxTime : restTime / charts.Length;
        }

        public int TotalScore => Entries.Sum(c => c.SessionScore);

        public TimeSpan TotalPlayTime =>
            TimeSpan.FromTicks(Entries.Select(e => e.Chart).Sum(c => c.Song.Duration.Ticks));

        /// <summary>
        ///     Whether the chart can enter the session: it must price above zero, and the window
        ///     must not be filled. A chart already in the session may enter again — the better play
        ///     is the one that stays (D39, march-of-murlocs.md §1) — and that replacement adds no
        ///     duration, so it is allowed even once the entered charts fill the window.
        /// </summary>
        public bool CanAdd(Chart chart)
        {
            if (_configuration.Scoring.GetScorelessScore(chart) == 0) return false;
            if (Replays(chart)) return true;
            // The window governs when a chart may start, not when it must finish — the closing
            // chart may overhang it, so the candidate's own duration never enters the test.
            return TotalPlayTime < _configuration.MaxTime;
        }

        /// <summary>The play this chart would replace: one is already in, and repeats are not separate entries.</summary>
        private Entry? Held(Chart chart)
        {
            return _configuration.AllowRepeats ? null : Entries.FirstOrDefault(e => IsSameChart(e.Chart, chart));
        }

        private bool Replays(Chart chart) => Held(chart) != null;

        /// <summary>A chart's identity here is song + type + level: the same song at another level is a different chart.</summary>
        private static bool IsSameChart(Chart a, Chart b)
        {
            return a.Level == b.Level && a.Type == b.Type && a.Song.Name == b.Song.Name;
        }

        public void Swap(Entry oldEntry, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            var index = Entries.IndexOf(oldEntry);
            if (index == -1) return;

            var basePoints = _configuration.Scoring.GetScore(oldEntry.Chart, score, plate, isBroken, false);
            var withBonus = _configuration.Scoring.GetScore(oldEntry.Chart, score, plate, isBroken);
            Entries[index] = oldEntry with
            {
                Score = score, Plate = plate, IsBroken = isBroken,
                SessionScore = (int)withBonus,
                BonusPoints = (int)(withBonus - basePoints)
            };
        }

        public void Remove(Entry entry)
        {
            Entries.Remove(entry);
        }

        /// <summary>
        ///     Enters a play. When the chart is already in the session and repeats are not separate
        ///     entries, only the better score stays (D39): a higher score replaces the held play, a
        ///     lower or equal one is dropped and the session is unchanged. Throws for a chart the
        ///     session cannot take at all.
        /// </summary>
        public AddOutcome Add(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            if (!CanAdd(chart))
            {
                throw new ArgumentException($"{chart.Song.Name} {chart.DifficultyString} is invalid for this session");
            }

            var held = Held(chart);
            if (held != null)
            {
                if (score <= held.Score) return AddOutcome.KeptExisting;

                Swap(held, score, plate, isBroken);
                return AddOutcome.Replaced;
            }

            var basePoints = _configuration.Scoring.GetScore(chart, score, plate, isBroken, false);
            var withBonus = _configuration.Scoring.GetScore(chart, score, plate, isBroken);
            Entries.Add(
                new Entry(chart, score, plate, isBroken,
                    (int)withBonus, (int)(withBonus - basePoints)));
            return AddOutcome.Added;
        }

        /// <summary>What <see cref="Add" /> did with a play.</summary>
        public enum AddOutcome
        {
            /// <summary>A new entry.</summary>
            Added,

            /// <summary>The chart was already in; this play scored higher and took its place.</summary>
            Replaced,

            /// <summary>The chart was already in with a score at least as high; nothing changed.</summary>
            KeptExisting
        }

        public sealed record Entry(Chart Chart, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken,
            int SessionScore, int BonusPoints)
        {
        }
    }
}