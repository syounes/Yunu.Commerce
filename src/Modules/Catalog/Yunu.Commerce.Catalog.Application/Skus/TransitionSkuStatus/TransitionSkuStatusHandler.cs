using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.TransitionSkuStatus;

/// <summary>
/// Orchestrates an explicit lifecycle Status transition for an existing Sku
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// The state machine itself is enforced by Sku.Activate/Block/Discontinue;
/// this handler only adds the cross-aggregate guard preventing a Sku from
/// leaving Archived while its owning Product is Archived, and persists the
/// change with an optimistic-concurrency retry loop against
/// <see cref="ISkuRepository.UpdateStatusAsync"/>. Sku status is never
/// propagated to/from Product (docs/adr/0010 preserved unchanged).
/// </summary>
public sealed class TransitionSkuStatusHandler
{
    private const int MaxRetries = 3;

    private readonly ISkuRepository _skuRepository;
    private readonly IProductRepository _productRepository;

    public TransitionSkuStatusHandler(ISkuRepository skuRepository, IProductRepository productRepository)
    {
        _skuRepository = skuRepository;
        _productRepository = productRepository;
    }

    public async Task HandleAsync(TransitionSkuStatusCommand command, CancellationToken cancellationToken)
    {
        var newStatus = ParseEnum<SkuStatus>(command.Status, nameof(command.Status));

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var sku = await _skuRepository.GetByIdAsync(new SkuId(command.SkuId), cancellationToken);
            if (sku is null)
            {
                throw new KeyNotFoundException($"Sku '{command.SkuId}' not found.");
            }

            if (newStatus != SkuStatus.Archived)
            {
                var product = await _productRepository.GetByIdAsync(sku.ProductId, cancellationToken);
                if (product is not null && product.Status == ProductStatus.Archived)
                {
                    throw new ProductArchivedException(
                        $"Sku '{command.SkuId}' cannot transition to '{newStatus}' because its Product '{sku.ProductId.Value}' is Archived.");
                }
            }

            var expectedCurrentStatus = sku.Status;

            switch (newStatus)
            {
                case SkuStatus.Active:
                    sku.Activate();
                    break;
                case SkuStatus.Inactive:
                    sku.Block();
                    break;
                case SkuStatus.Archived:
                    sku.Discontinue();
                    break;
                default:
                    throw new ArgumentException($"Invalid Status: '{command.Status}'.", nameof(command));
            }

            var updated = await _skuRepository.UpdateStatusAsync(
                sku.Id,
                expectedCurrentStatus,
                newStatus,
                cancellationToken);

            if (updated)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Could not transition Sku '{command.SkuId}' status due to a concurrent update. Please retry.");
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
