using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Thrown by <see cref="CreateProductProposalHandler"/> when the catalog
/// intent resolution outcome does not meet the criteria required to persist
/// a <see cref="Yunu.Commerce.Catalog.Domain.ProductProposals.ProductProposal"/>
/// (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). Carries the full resolution result so the Host boundary can
/// map it to a 422 Unprocessable Entity response with status, warnings and a
/// resolution summary, without persisting anything.
/// </summary>
public sealed class ProductProposalResolutionException : Exception
{
    public CatalogIntentResolutionResult Resolution { get; }

    public ProductProposalResolutionException(CatalogIntentResolutionResult resolution)
        : base("The catalog intent resolution outcome is not ready to be persisted as a ProductProposal.")
    {
        Resolution = resolution;
    }
}
