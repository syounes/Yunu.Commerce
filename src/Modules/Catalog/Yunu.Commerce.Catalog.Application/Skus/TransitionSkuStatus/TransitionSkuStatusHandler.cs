using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.TransitionSkuStatus;

/// <summary>
/// Orchestrates an explicit lifecycle Status transition for an existing Sku
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// The state machine itself is enforced by Sku.Activate/Block/Discontinue.
///
/// This handler operates strictly on the state view it loaded for this
/// attempt: if the conditional persistence write does not match (another
/// writer already changed the Sku concurrently), it does NOT reload the
/// Aggregate and reinterpret the original command against the new state; it
/// throws <see cref="SkuStatusConcurrencyConflictException"/> instead
/// (first-writer-wins), translated by the HTTP layer to 409 Conflict.
///
/// A Sku being (re)activated/blocked (i.e. transitioning to anything other
/// than Archived) races with a concurrent Product Archive for the
/// cross-aggregate invariant "Product Archived ⇒ no non-Archived Sku"
/// (docs task: "V11 - Product/Sku Lifecycle Concurrency"); that case is
/// delegated to <see cref="IProductSkuConcurrencyCoordinator"/>, which
/// atomically re-checks the owning Product is not Archived and commits the
/// Sku transition inside a single MongoDB transaction. A Sku transitioning
/// to Archived has no cross-aggregate concern (an Archived Product's Skus
/// may still individually become Archived) and is persisted directly through
/// <see cref="ISkuRepository.UpdateStatusAsync"/>. Sku status is never
/// propagated to/from Product (docs/adr/0010 preserved unchanged).
/// </summary>
public sealed class TransitionSkuStatusHandler
{
    private readonly ISkuRepository _skuRepository;
    private readonly IProductSkuConcurrencyCoordinator _concurrencyCoordinator;

    public TransitionSkuStatusHandler(ISkuRepository skuRepository, IProductSkuConcurrencyCoordinator concurrencyCoordinator)
    {
        _skuRepository = skuRepository;
        _concurrencyCoordinator = concurrencyCoordinator;
    }

    public async Task HandleAsync(TransitionSkuStatusCommand command, CancellationToken cancellationToken)
    {
        var newStatus = ParseEnum<SkuStatus>(command.Status, nameof(command.Status));

        var sku = await _skuRepository.GetByIdAsync(new SkuId(command.SkuId), cancellationToken);
        if (sku is null)
        {
            throw new KeyNotFoundException($"Sku '{command.SkuId}' not found.");
        }

        var expectedCurrentStatus = sku.Status;

        // Validates the transition itself (throws InvalidSkuStatusTransitionException
        // for an illegal transition) against the state loaded for this attempt only.
        ApplyTransition(sku, newStatus, command);

        if (newStatus == SkuStatus.Archived)
        {
            var updated = await _skuRepository.UpdateStatusAsync(
                sku.Id,
                expectedCurrentStatus,
                newStatus,
                cancellationToken);

            if (!updated)
            {
                throw new SkuStatusConcurrencyConflictException(
                    $"Sku '{command.SkuId}' was concurrently modified by another writer. Reload the current state and retry explicitly.");
            }

            return;
        }

        var result = await _concurrencyCoordinator.TransitionSkuIfProductNotArchivedAsync(
            sku.Id,
            expectedCurrentStatus,
            newStatus,
            cancellationToken);

        switch (result)
        {
            case SkuTransitionCoordinationResult.Transitioned:
                return;
            case SkuTransitionCoordinationResult.SkuNotFound:
                throw new KeyNotFoundException($"Sku '{command.SkuId}' not found.");
            case SkuTransitionCoordinationResult.ProductNotFound:
                throw new KeyNotFoundException($"Sku '{command.SkuId}' references a Product that no longer exists.");
            case SkuTransitionCoordinationResult.ProductArchived:
                throw new ProductArchivedException(
                    $"Sku '{command.SkuId}' cannot transition to '{newStatus}' because its Product is Archived.");
            case SkuTransitionCoordinationResult.ConcurrencyConflict:
                throw new SkuStatusConcurrencyConflictException(
                    $"Sku '{command.SkuId}' was concurrently modified by another writer. Reload the current state and retry explicitly.");
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported Sku transition coordination result.");
        }
    }

    private static void ApplyTransition(Sku sku, SkuStatus newStatus, TransitionSkuStatusCommand command)
    {
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
