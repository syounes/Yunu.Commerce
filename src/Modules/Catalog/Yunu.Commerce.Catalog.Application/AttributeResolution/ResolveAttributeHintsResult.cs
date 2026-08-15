namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Aggregate outcome of resolving a batch of attribute hints (docs task:
/// "Semantic attribute hint resolution"). <see cref="Attributes"/> preserves
/// the original input order regardless of internal parallelism.
/// <see cref="AllResolved"/> is true only when every hint is
/// <see cref="AttributeResolutionStatus.Resolved"/> and, for Enum attributes,
/// a valid option was also resolved.
/// </summary>
public sealed record ResolveAttributeHintsResult(
    IReadOnlyList<ResolvedAttributeHint> Attributes,
    bool AllResolved);
