namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Summary of the catalog intent resolution outcome that produced a
/// <see cref="ProductProposal"/> (docs task: "Catalog intent resolution
/// orchestration" - proposal persistence). Deliberately independent from any
/// HTTP DTO: the Domain must not depend on Host contracts.
/// </summary>
public sealed record ProposalResolution(
    string Status,
    bool CategoryResolved,
    bool AllAttributesResolved,
    bool ReadyForProposal,
    decimal IntentConfidence,
    IReadOnlyCollection<string> Warnings);
