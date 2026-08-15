namespace Yunu.Commerce.Api.AI.CatalogIntentResolution;

/// <summary>
/// HTTP request contract for POST /api/ai/catalog/resolve (docs task:
/// "Catalog intent resolution orchestration").
/// </summary>
public sealed class CatalogIntentResolutionHttpRequest
{
    public required string Input { get; init; }

    public string Locale { get; init; } = "pt-BR";
}
