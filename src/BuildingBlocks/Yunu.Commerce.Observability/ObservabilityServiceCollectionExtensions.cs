using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Yunu.Commerce.Observability;

/// <summary>
/// Reusable OpenTelemetry bootstrap shared across Hosts (docs §13).
/// Exporters are minimal (console) until a collector endpoint is defined by
/// deployment configuration; this must not contain business-specific telemetry logic.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddYunuObservability(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(serviceName)
                .ConfigureResource(resource => resource.AddService(serviceName))
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .ConfigureResource(resource => resource.AddService(serviceName))
                .AddConsoleExporter());

        return services;
    }
}
