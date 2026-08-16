using Yunu.Commerce.Catalog.Domain.ProductProposals.Events;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// ProductProposal Aggregate Root (docs task: "Catalog intent resolution
/// orchestration" - proposal persistence). Represents a not-yet-confirmed
/// suggestion produced by the catalog intent resolution pipeline
/// (Intent Rewriter + Google Category Resolution + Attribute Hint
/// Resolution); it is NOT a canonical <see cref="Product"/> nor a canonical
/// Sku. Confirmation, rejection and conversion into a real Product/Sku are
/// deferred to a future use case (docs task restriction): this Aggregate
/// only supports being created and rehydrated at this phase.
/// </summary>
public sealed class ProductProposal
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<ProposedSku> _skus = new();

    public ProductProposalId Id { get; }

    public ProductProposalStatus Status { get; private set; }

    public string Locale { get; }

    public ProposalSource Source { get; }

    public ProposedProduct Product { get; }

    public IReadOnlyCollection<ProposedSku> Skus => _skus;

    public ProposalResolution Resolution { get; }

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ConfirmedAtUtc { get; private set; }

    public DateTime? ConvertedAtUtc { get; private set; }

    public ProductId? CreatedProductId { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private ProductProposal(
        ProductProposalId id,
        ProductProposalStatus status,
        string locale,
        ProposalSource source,
        ProposedProduct product,
        ProposalResolution resolution,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        DateTime? confirmedAtUtc,
        DateTime? convertedAtUtc,
        ProductId? createdProductId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(resolution);

        Id = id;
        Status = status;
        Locale = locale;
        Source = source;
        Product = product;
        Resolution = resolution;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ConfirmedAtUtc = confirmedAtUtc;
        ConvertedAtUtc = convertedAtUtc;
        CreatedProductId = createdProductId;
    }

    /// <summary>
    /// Creates a new ProductProposal, always starting as
    /// <see cref="ProductProposalStatus.AwaitingReview"/> (docs task:
    /// "Catalog intent resolution orchestration" - proposal persistence).
    /// Confirmation/conversion/rejection are not modeled yet.
    /// </summary>
    public static ProductProposal Create(
        ProductProposalId id,
        string locale,
        ProposalSource source,
        ProposedProduct product,
        IEnumerable<ProposedSku> skus,
        ProposalResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var nowUtc = DateTime.UtcNow;

        var proposal = new ProductProposal(
            id,
            ProductProposalStatus.AwaitingReview,
            locale,
            source,
            product,
            resolution,
            nowUtc,
            nowUtc,
            confirmedAtUtc: null,
            convertedAtUtc: null,
            createdProductId: null);

        proposal._skus.AddRange(skus);

        proposal._domainEvents.Add(new ProductProposalCreatedDomainEvent(id));

        return proposal;
    }

    /// <summary>
    /// Rehydrates a ProductProposal from persisted state without raising
    /// creation-time domain events. Used exclusively by Infrastructure
    /// persistence mappers.
    /// </summary>
    public static ProductProposal Hydrate(
        ProductProposalId id,
        ProductProposalStatus status,
        string locale,
        ProposalSource source,
        ProposedProduct product,
        IEnumerable<ProposedSku> skus,
        ProposalResolution resolution,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        DateTime? confirmedAtUtc,
        DateTime? convertedAtUtc,
        ProductId? createdProductId)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var proposal = new ProductProposal(
            id,
            status,
            locale,
            source,
            product,
            resolution,
            createdAtUtc,
            updatedAtUtc,
            confirmedAtUtc,
            convertedAtUtc,
            createdProductId);

        proposal._skus.AddRange(skus);

        return proposal;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
