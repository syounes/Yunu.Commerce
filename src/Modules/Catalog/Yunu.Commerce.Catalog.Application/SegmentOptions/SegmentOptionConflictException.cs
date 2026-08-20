namespace Yunu.Commerce.Catalog.Application.SegmentOptions;

/// <summary>
/// Thrown when a Create/Update operation on a
/// <see cref="Yunu.Commerce.Catalog.Domain.Segments.SegmentOption"/> would
/// conflict with an existing Code or NormalizedName within the same
/// SegmentDefinition (docs task: "Implementar Domain + Write-Side de
/// SegmentOption"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.SegmentDefinitionConflictException"/>.
/// </summary>
public sealed class SegmentOptionConflictException : Exception
{
    public SegmentOptionConflictException(string message) : base(message)
    {
    }
}
