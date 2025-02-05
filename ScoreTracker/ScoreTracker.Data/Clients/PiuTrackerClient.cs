using System.Text;
using System.Text.Json;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Clients
{
    public sealed class PiuTrackerClient : IPiuTrackerClient
    {
        private readonly HttpClient _client;

        public PiuTrackerClient(HttpClient client)
        {
            _client = client;
        }

        public async Task SyncData(Name gameTag, string sid, CancellationToken cancellationToken)
        {
            var content = new StringContent(JsonSerializer.Serialize(new
            {
                sid
            }), Encoding.UTF8, "application/json");
            var gameId = gameTag.ToString().Split("#").First().Trim();
            var number = gameTag.ToString().Split("#").Last().Trim();
            _client.Timeout = TimeSpan.FromMinutes(5);
            var result = await _client.PostAsync($"https://piutracker.app:3002/api/sync/{gameId}/{number}", content,
                cancellationToken);
            result.EnsureSuccessStatusCode();
        }
    }
}
