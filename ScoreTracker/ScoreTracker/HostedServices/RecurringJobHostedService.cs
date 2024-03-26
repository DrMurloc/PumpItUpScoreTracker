using System.Globalization;
using MassTransit;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.Web.HostedServices
{
    public sealed class RecurringJobHostedService : IHostedService
    {
        private readonly IBus _bus;

        public RecurringJobHostedService(IBus bus)
        {
            _bus = bus;
        }

        private Timer? _timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(_ => RunJobs().RunSynchronously(), null, getJobRunDelay("07:00"),
                new TimeSpan(24, 0, 0));
            return Task.CompletedTask;
        }

        private async Task RunJobs()
        {
            await _bus.Publish(new ProcessScoresTiersListCommand());
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            _timer = null;
            return Task.CompletedTask;
        }

        private static TimeSpan getScheduledParsedTime(string jobStartTime)
        {
            string[] formats = { @"hh\:mm\:ss", "hh\\:mm" };
            TimeSpan.TryParseExact(jobStartTime, formats, CultureInfo.InvariantCulture, out var ScheduledTimespan);
            return ScheduledTimespan;
        }

        private static TimeSpan getJobRunDelay(string jobStartTime)
        {
            var scheduledParsedTime = getScheduledParsedTime(jobStartTime);
            var curentTimeOftheDay = TimeSpan.Parse(DateTime.Now.TimeOfDay.ToString("hh\\:mm"));
            var delayTime = scheduledParsedTime >= curentTimeOftheDay
                ? scheduledParsedTime - curentTimeOftheDay // Initial Run, when ETA is within 24 hours
                : new TimeSpan(24, 0, 0) - curentTimeOftheDay + scheduledParsedTime; // For every other subsequent runs
            return delayTime;
        }
    }
}
