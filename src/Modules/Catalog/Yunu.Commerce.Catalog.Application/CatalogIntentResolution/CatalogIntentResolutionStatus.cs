namespace Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

/// <summary>
/// Consolidated end-to-end outcome of interpreting a natural-language catalog
/// intent (docs task: "Catalog intent resolution orchestration"). These are
/// functional outcomes, not exceptions.
/// </summary>
public enum CatalogIntentResolutionStatus
{
    /// <summary>
    /// Category resolved and validated, and every attribute hint resolved
    /// (including options for Enum attributes).
    /// </summary>
    Resolved,

    /// <summary>
    /// Category or at least one attribute hint is Ambiguous, or the category
    /// was NotFound but attributes could still be partially resolved: user
    /// input/clarification is required before a proposal can be created.
    /// </summary>
    NeedsClarification,

    /// <summary>
    /// Category could not be found and no attribute could compensate for it,
    /// or every attribute hint is NotFound.
    /// </summary>
    NotFound,

    /// <summary>
    /// The request itself was invalid (e.g. empty input).
    /// </summary>
    Invalid
}
