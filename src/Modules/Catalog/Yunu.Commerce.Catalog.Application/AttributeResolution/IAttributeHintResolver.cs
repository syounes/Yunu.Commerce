namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Application port that resolves a batch of textual attribute hints into
/// official Catalog.AttributeDefinitions / Catalog.AttributeOptions
/// references (docs task: "Semantic attribute hint resolution"). This is the
/// only entry point Hosts depend on; the resolution strategy (exact match,
/// semantic search, SQL Server validation) is an internal Application
/// concern. Never persists anything.
/// </summary>
public interface IAttributeHintResolver
{
    Task<ResolveAttributeHintsResult> ResolveAsync(
        ResolveAttributeHintsRequest request,
        CancellationToken cancellationToken);
}
