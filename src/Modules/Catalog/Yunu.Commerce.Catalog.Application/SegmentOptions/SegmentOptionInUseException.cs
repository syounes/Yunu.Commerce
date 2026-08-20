namespace Yunu.Commerce.Catalog.Application.SegmentOptions;

/// <summary>
/// Thrown when an Archive transition is attempted against a
/// <see cref="Yunu.Commerce.Catalog.Domain.Segments.SegmentOption"/> that is
/// still in use (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards de
/// Segments"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.SegmentDefinitionInUseException"/>.
/// </summary>
public sealed class SegmentOptionInUseException : Exception
{
    public SegmentOptionInUseException(string message) : base(message)
    {
    }
}
