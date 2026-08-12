using Yunu.Commerce.Catalog.Domain.Products.Skus.Events;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Skus;

/// <summary>
/// Sku Aggregate Root (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Sku is an independent Aggregate that references its owning Product only by
/// identity (<see cref="ProductId"/>). Sku has its own lifecycle, persistence
/// boundary and repository contract; it is no longer constructed or owned by
/// the Product Aggregate.
/// </summary>
public sealed class Sku
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public SkuId Id { get; }

    public ProductId ProductId { get; }

    public SkuCode Code { get; }

    public string? Gtin { get; private set; }

    public SkuStatus Status { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private Sku(SkuId id, ProductId productId, SkuCode code, string? gtin, SkuStatus status)
    {
        Id = id;
        ProductId = productId;
        Code = code;
        Gtin = gtin;
        Status = status;
    }

    /// <summary>
    /// Creates a new Sku for an existing Product identity. A Sku cannot exist
    /// without a valid <see cref="ProductId"/> (docs/adr/0010 §5).
    /// </summary>
    public static Sku Create(
        SkuId id,
        ProductId productId,
        SkuCode code,
        string? gtin = null,
        SkuStatus status = SkuStatus.Draft)
    {
        var sku = new Sku(id, productId, code, gtin, status);

        sku._domainEvents.Add(new SkuCreatedDomainEvent(id, productId));

        return sku;
    }

    /// <summary>
    /// Transitions the Sku to Active. No documented invariant currently restricts
    /// which prior statuses may activate; kept simple until lifecycle rules are
    /// formally defined (docs/domains/catalog.md §20).
    /// </summary>
    public void Activate()
    {
        if (Status == SkuStatus.Active)
        {
            return;
        }

        Status = SkuStatus.Active;
        _domainEvents.Add(new SkuActivatedDomainEvent(Id, ProductId));
    }

    /// <summary>
    /// Transitions the Sku to Inactive ("blocked").
    /// </summary>
    public void Block()
    {
        if (Status == SkuStatus.Inactive)
        {
            return;
        }

        Status = SkuStatus.Inactive;
        _domainEvents.Add(new SkuBlockedDomainEvent(Id, ProductId));
    }

    /// <summary>
    /// Transitions the Sku to Archived ("discontinued").
    /// </summary>
    public void Discontinue()
    {
        if (Status == SkuStatus.Archived)
        {
            return;
        }

        Status = SkuStatus.Archived;
        _domainEvents.Add(new SkuDiscontinuedDomainEvent(Id, ProductId));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
