namespace Yunu.Commerce.Worker;

/// <summary>
/// Placeholder background service. Concrete Kafka consumers, Outbox and Inbox
/// processors will be added incrementally once module Infrastructure adapters
/// are implemented (docs/architecture/06-solution-structure.md §8).
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Yunu.Commerce.Worker heartbeat at {TimestampUtc}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
