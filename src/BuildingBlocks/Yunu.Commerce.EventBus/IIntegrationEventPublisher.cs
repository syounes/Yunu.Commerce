using Yunu.Commerce.Contracts;

namespace Yunu.Commerce.EventBus;

/// <summary>
/// Messaging abstraction for publishing integration events (docs §12).
/// Broker-specific implementations (e.g. Kafka) must live in module Infrastructure
/// projects only, never in Domain or Application.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}
