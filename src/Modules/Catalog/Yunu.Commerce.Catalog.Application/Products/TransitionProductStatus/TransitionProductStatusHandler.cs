using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Products.TransitionProductStatus;

/// <summary>
/// Orchestrates an explicit lifecycle Status transition for an existing
/// Product (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// The state machine itself is enforced by <see cref="Product.TransitionTo"/>.
///
/// This handler operates strictly on the state view it loaded for this
/// attempt: if the conditional persistence write does not match (another
/// writer already changed the Product concurrently), it does NOT reload the
/// Aggregate and reinterpret the original command against the new state; it
/// throws <see cref="ProductStatusConcurrencyConflictException"/> instead
/// (first-writer-wins), translated by the HTTP layer to 409 Conflict.
///
/// Archiving a Product races with Sku creation/(re)activation for the
/// cross-aggregate invariant "Product Archived ⇒ no non-Archived Sku"
/// (docs task: "V11 - Product/Sku Lifecycle Concurrency"); that specific
/// transition is delegated to <see cref="IProductSkuConcurrencyCoordinator"/>,
/// which atomically re-checks for a non-Archived Sku and commits the Archive
/// inside a single MongoDB transaction. Non-Archive transitions have no
/// cross-aggregate concern and are persisted directly through
/// <see cref="IProductRepository.UpdateStatusAsync"/>.
/// </summary>
public sealed class TransitionProductStatusHandler
{
    private readonly IProductRepository _productRepository;
    private readonly IProductSkuConcurrencyCoordinator _concurrencyCoordinator;

    public TransitionProductStatusHandler(
        IProductRepository productRepository,
        IProductSkuConcurrencyCoordinator concurrencyCoordinator)
    {
        _productRepository = productRepository;
        _concurrencyCoordinator = concurrencyCoordinator;
    }

    public async Task HandleAsync(TransitionProductStatusCommand command, CancellationToken cancellationToken)
    {
        var newStatus = ParseEnum<ProductStatus>(command.Status, nameof(command.Status));

        var product = await _productRepository.GetByIdAsync(new ProductId(command.ProductId), cancellationToken);
        if (product is null)
        {
            throw new KeyNotFoundException($"Product '{command.ProductId}' not found.");
        }

        var expectedCurrentStatus = product.Status;

        // Validates the transition itself (throws InvalidProductStatusTransitionException
        // for an illegal transition) against the state loaded for this attempt only.
        product.TransitionTo(newStatus);

        if (newStatus == ProductStatus.Archived)
        {
            var result = await _concurrencyCoordinator.ArchiveProductAsync(product.Id, expectedCurrentStatus, cancellationToken);

            switch (result)
            {
                case ArchiveProductCoordinationResult.Archived:
                    return;
                case ArchiveProductCoordinationResult.ProductNotFound:
                    throw new KeyNotFoundException($"Product '{command.ProductId}' not found.");
                case ArchiveProductCoordinationResult.NonArchivedSkuExists:
                    throw new ProductHasNonArchivedSkusException(
                        $"Product '{command.ProductId}' has at least one non-Archived Sku and cannot be archived.");
                case ArchiveProductCoordinationResult.ConcurrencyConflict:
                    throw new ProductStatusConcurrencyConflictException(
                        $"Product '{command.ProductId}' was concurrently modified by another writer. Reload the current state and retry explicitly.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported ArchiveProduct coordination result.");
            }
        }

        var updated = await _productRepository.UpdateStatusAsync(
            product.Id,
            expectedCurrentStatus,
            newStatus,
            cancellationToken);

        if (!updated)
        {
            throw new ProductStatusConcurrencyConflictException(
                $"Product '{command.ProductId}' was concurrently modified by another writer. Reload the current state and retry explicitly.");
        }
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
