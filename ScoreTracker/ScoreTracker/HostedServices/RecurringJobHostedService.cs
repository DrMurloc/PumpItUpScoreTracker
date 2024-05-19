using MassTransit;
using ScoreTracker.Domain.Events;
using static ScoreTracker.Web.HostedServices.RecurringJobHostedService;

namespace ScoreTracker.Web.HostedServices;

public sealed class RecurringJobHostedService : IHostedService,
    IConsumer<RescheduleMessages>
{
    private readonly IBus _bus;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public sealed class RescheduleMessages
    {
    }

    public RecurringJobHostedService(IBus bus, IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _bus = bus;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _bus.Publish(new RescheduleMessages(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<RescheduleMessages> context)
    {
        if (_configuration["PreventRecurringJobs"] == "true") return;
        await using var scope = _serviceProvider.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IMessageScheduler>();
        var nextDate = DateTime.Now;
        if (nextDate.Hour > 2) nextDate += TimeSpan.FromDays(1);

        await scheduler.SchedulePublish(new DateTime(nextDate.Year, nextDate.Month, nextDate.Day, 2, 0, 0),
            new ProcessScoresTiersListCommand(), context.CancellationToken);

        await scheduler.SchedulePublish(new DateTime(nextDate.Year, nextDate.Month, nextDate.Day, 2, 30, 0),
            new UpdateBountiesEvent(), context.CancellationToken);

        await scheduler.SchedulePublish(new DateTime(nextDate.Year, nextDate.Month, nextDate.Day, 3, 0, 0),
            new CalculateScoringDifficultyEvent(), context.CancellationToken);

        await scheduler.SchedulePublish(new DateTime(nextDate.Year, nextDate.Month, nextDate.Day, 3, 30, 0),
            new RescheduleMessages(), context.CancellationToken);
    }
}