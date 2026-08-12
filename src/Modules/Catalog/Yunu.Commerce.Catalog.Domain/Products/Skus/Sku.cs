namespace Yunu.Commerce.Catalog.Domain.Products.Skus;

/// <summary>
/// Sku Entity, owned by the Product Aggregate (docs/domains/catalog.md §6).
/// A Sku cannot be constructed independently of its owning Product; construction
/// is internal and only reachable through <see cref="Product.AddSku"/>.
/// No lifecycle transition behavior is implemented in this phase; status is set
/// only at construction time (docs/domains/catalog.md §20).
/// </summary>
public sealed class Sku
{
    public SkuId Id { get; }

    public SkuCode Code { get; }

    public SkuStatus Status { get; }

    internal Sku(SkuId id, SkuCode code, SkuStatus status)
    {
        Id = id;
        Code = code;
        Status = status;
    }
}
