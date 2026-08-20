namespace Yunu.Commerce.Catalog.Application.SegmentOptions;

/// <summary>
/// Thrown when creation of a
/// <see cref="Yunu.Commerce.Catalog.Domain.Segments.SegmentOption"/> is
/// attempted under a parent
/// <see cref="Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinition"/> that
/// is Archived (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards de
/// Segments" - "Regra minima obrigatoria": an Archived Definition cannot
/// receive new structural values).
/// </summary>
public sealed class SegmentDefinitionArchivedException : Exception
{
    public SegmentDefinitionArchivedException(string message) : base(message)
    {
    }
}
