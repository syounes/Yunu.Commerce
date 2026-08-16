namespace Yunu.Commerce.Api.ProductProposals;

/// <summary>
/// HTTP request contract for POST /api/catalog/product-proposals (docs task:
/// "Catalog intent resolution orchestration" - proposal persistence).
/// Mirrors the natural-language input already accepted by
/// POST /api/ai/catalog/resolve.
/// </summary>
public sealed class CreateProductProposalRequest
{
    public required string Input { get; init; }

    public string Locale { get; init; } = "pt-BR";
}
