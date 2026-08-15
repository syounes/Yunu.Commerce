using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.Api.AI.AttributeResolution;

/// <summary>
/// HTTP request for POST /api/ai/attributes/resolve (docs task: "Semantic
/// attribute hint resolution"). Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeResolution.ResolveAttributeHintsRequest"/>
/// as a distinct HTTP contract.
/// </summary>
public sealed class ResolveAttributeHintsHttpRequest
{
    public required IReadOnlyList<AttributeHintDto> AttributeHints { get; init; }

    public long? GoogleCategoryId { get; init; }

    public string? Locale { get; init; }
}

public sealed class AttributeHintDto
{
    public required string RawName { get; init; }

    public string? RawValue { get; init; }

    public AttributeHint ToAttributeHint() => new(RawName, RawValue);
}
