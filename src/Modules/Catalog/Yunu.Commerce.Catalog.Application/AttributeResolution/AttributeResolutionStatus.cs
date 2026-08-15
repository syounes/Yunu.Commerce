namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Outcome of resolving a single textual attribute hint (rawName/rawValue)
/// into an official Catalog.AttributeDefinitions / Catalog.AttributeOptions
/// reference (docs task: "Semantic attribute hint resolution"). These are
/// functional outcomes, not exceptions: a hint that cannot be resolved safely
/// is reported back to the caller rather than throwing.
/// </summary>
public enum AttributeResolutionStatus
{
    /// <summary>
    /// The attribute definition (and, for Enum attributes, the option) was
    /// resolved with sufficient confidence and validated against SQL Server.
    /// </summary>
    Resolved,

    /// <summary>
    /// The best candidates were too close in similarity (insufficient margin)
    /// to safely pick one, or the definition could be resolved but its Enum
    /// value could not be resolved unambiguously.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// No candidate met the minimum similarity threshold, or no candidate
    /// could be validated against SQL Server.
    /// </summary>
    NotFound,

    /// <summary>
    /// The attribute definition was resolved, but rawValue could not be
    /// interpreted according to the definition's DataType.
    /// </summary>
    InvalidValue
}
