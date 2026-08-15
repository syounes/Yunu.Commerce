using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Input for resolving a batch of textual attribute hints produced by the
/// Intent Rewriter (<see cref="AttributeHint"/>) into official
/// Catalog.AttributeDefinitions / Catalog.AttributeOptions references
/// (docs task: "Semantic attribute hint resolution"). Hints are resolved as a
/// batch to avoid one round-trip per hint against pgvector/SQL Server.
/// </summary>
public sealed record ResolveAttributeHintsRequest(
    IReadOnlyList<AttributeHint> AttributeHints,
    long? GoogleCategoryId = null,
    string Locale = "pt-BR");
