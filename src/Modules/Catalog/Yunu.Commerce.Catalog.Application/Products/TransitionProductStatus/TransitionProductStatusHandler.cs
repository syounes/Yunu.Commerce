using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Products.TransitionProductStatus;

/// <summary>
/// Orchestrates an explicit lifecycle Status transition for an existing
/// Product (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// The state machine itself is enforced by <see cref="Product.TransitionTo"/>;
/// this handler only adds the cross-aggregate Archive guard (no non-Archived
/// Sku may exist) and persists the change with an optimistic-concurrency retry
/// loop against <see cref="IProductRepository.UpdateStatusAsync"/>.
/// </summary>
public sealed class TransitionProductStatusHandler
{
    private const int MaxRetries = 3;

    private readonly IProductRepository _productRepository;
    private readonly ISkuRepository _skuRepository;

    public TransitionProductStatusHandler(IProductRepository productRepository, ISkuRepository skuRepository)
    {
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    public async Task HandleAsync(TransitionProductStatusCommand command, CancellationToken cancellationToken)
    {
        var newStatus = ParseEnum<ProductStatus>(command.Status, nameof(command.Status));

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var product = await _productRepository.GetByIdAsync(new ProductId(command.ProductId), cancellationToken);
            if (product is null)
            {
                throw new KeyNotFoundException($"Product '{command.ProductId}' not found.");
            }

            if (newStatus == ProductStatus.Archived && product.Status != ProductStatus.Archived)
            {
                if (await _skuRepository.ExistsNonArchivedByProductIdAsync(product.Id, cancellationToken))
                {
                    throw new ProductHasNonArchivedSkusException(
                        $"Product '{command.ProductId}' has at least one non-Archived Sku and cannot be archived.");
                }
            }

            var expectedCurrentStatus = product.Status;
            product.TransitionTo(newStatus);

            var updated = await _productRepository.UpdateStatusAsync(
                product.Id,
                expectedCurrentStatus,
                newStatus,
                cancellationToken);

            if (updated)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Could not transition Product '{command.ProductId}' status due to a concurrent update. Please retry.");
    }

    private static TEnum ParseEnum<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid {paramName}: '{value}'.", paramName);
        }

        return parsed;
    }
}
