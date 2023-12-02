using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Data.Repositories
{
    public sealed class FakeMatchRepository : IMatchRepository
    {
        private static readonly IDictionary<string, MatchView> Matches = new Dictionary<string, MatchView>();

        public Task<MatchView> GetMatch(Name matchName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Matches[matchName]);
        }

        public Task<IEnumerable<MatchView>> GetAllMatches(CancellationToken cancellationToken)
        {
            return Task.FromResult(Matches.Values.AsEnumerable());
        }

        public Task SaveMatch(MatchView matchView, CancellationToken cancellationToken)
        {
            Matches[matchView.MatchName] = matchView;

            return Task.CompletedTask;
        }

        private static readonly IDictionary<string, RandomSettings> Settings = new Dictionary<string, RandomSettings>();

        public Task SaveRandomSettings(Name settingsName, RandomSettings settings, CancellationToken cancellationToken)
        {
            Settings[settingsName] = settings;
            return Task.CompletedTask;
        }

        public Task<RandomSettings> GetRandomSettings(Name settingsName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Settings[settingsName]);
        }

        public Task<IEnumerable<(Name name, RandomSettings settings)>> GetAllRandomSettings(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Settings.Select(kv => ((Name)kv.Key, kv.Value)));
        }
    }
}
