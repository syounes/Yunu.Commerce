using Microsoft.Extensions.Logging;

namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Generic, provider-neutral SourceTaxonomy import orchestrator
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §9). Coordinates:
/// SourceTaxonomy lookup, adapter resolution, import-history lifecycle,
/// snapshot loading/validation, source-identity safety checks, and atomic
/// SQL synchronization. Contains no provider-specific branching: adapters
/// are resolved purely by <see cref="ISourceTaxonomyAdapter.AdapterCode"/>.
/// </summary>
public sealed class SourceTaxonomyImportOrchestrator
{
    private readonly ISourceTaxonomyRepository _sourceTaxonomyRepository;
    private readonly IReadOnlyCollection<ISourceTaxonomyAdapter> _adapters;
    private readonly ISourceTaxonomyImportStore _importStore;
    private readonly ISourceTaxonomySynchronizationStore _synchronizationStore;
    private readonly ISourceTaxonomyImportGuard _importGuard;
    private readonly ILogger<SourceTaxonomyImportOrchestrator> _logger;

    public SourceTaxonomyImportOrchestrator(
        ISourceTaxonomyRepository sourceTaxonomyRepository,
        IEnumerable<ISourceTaxonomyAdapter> adapters,
        ISourceTaxonomyImportStore importStore,
        ISourceTaxonomySynchronizationStore synchronizationStore,
        ISourceTaxonomyImportGuard importGuard,
        ILogger<SourceTaxonomyImportOrchestrator> logger)
    {
        _sourceTaxonomyRepository = sourceTaxonomyRepository;
        _adapters = adapters.ToArray();
        _importStore = importStore;
        _synchronizationStore = synchronizationStore;
        _importGuard = importGuard;
        _logger = logger;
    }

    public async Task<SourceTaxonomyImportResult> ImportAsync(
        long sourceTaxonomyId,
        string adapterCode,
        CancellationToken cancellationToken)
    {
        using var lockToken = _importGuard.TryAcquire(sourceTaxonomyId);

        if (lockToken is null)
        {
            throw new SourceTaxonomyImportInProgressException(sourceTaxonomyId);
        }

        var descriptor = await _sourceTaxonomyRepository.GetByIdAsync(sourceTaxonomyId, cancellationToken)
            ?? throw new SourceTaxonomyNotFoundException(sourceTaxonomyId);

        if (!descriptor.IsActive)
        {
            throw new SourceTaxonomyInactiveException(sourceTaxonomyId);
        }

        var adapter = ResolveAdapter(adapterCode);

        var startedAtUtc = DateTime.UtcNow;

        var importId = await _importStore.StartAsync(
            sourceTaxonomyId,
            adapterCode,
            descriptor.SourceUri,
            descriptor.ExternalVersion,
            descriptor.SourceChecksum,
            startedAtUtc,
            cancellationToken);

        _logger.LogInformation(
            "SourceTaxonomy import {ImportId} started for SourceTaxonomy {SourceTaxonomyId} using adapter {AdapterCode}",
            importId,
            sourceTaxonomyId,
            adapterCode);

        try
        {
            var context = new SourceTaxonomyImportContext
            {
                SourceTaxonomyId = descriptor.SourceTaxonomyId,
                Code = descriptor.Code,
                ProviderCode = descriptor.ProviderCode,
                ScopeCode = descriptor.ScopeCode,
                ExternalTaxonomyId = descriptor.ExternalTaxonomyId,
                CurrentExternalVersion = descriptor.ExternalVersion,
                DefaultLanguage = descriptor.DefaultLanguage,
                SourceUri = descriptor.SourceUri,
                CurrentSourceChecksum = descriptor.SourceChecksum
            };

            var snapshot = await adapter.LoadAsync(context, cancellationToken);

            SourceTaxonomySnapshotValidator.Validate(snapshot);

            ValidateSourceIdentity(descriptor, snapshot.Descriptor);

            var synchronizationResult = await _synchronizationStore.ApplyAsync(
                sourceTaxonomyId,
                importId,
                snapshot,
                startedAtUtc,
                cancellationToken);

            var completedAtUtc = DateTime.UtcNow;

            _logger.LogInformation(
                "SourceTaxonomy import {ImportId} completed. NodeCount={NodeCount} Inserted={Inserted} Updated={Updated} Deactivated={Deactivated} SkippedByChecksum={Skipped}",
                importId,
                synchronizationResult.NodeCount,
                synchronizationResult.InsertedCount,
                synchronizationResult.UpdatedCount,
                synchronizationResult.DeactivatedCount,
                synchronizationResult.WasSkippedByChecksum);

            return new SourceTaxonomyImportResult
            {
                ImportId = importId,
                SourceTaxonomyId = sourceTaxonomyId,
                AdapterCode = adapterCode,
                NodeCount = synchronizationResult.NodeCount,
                InsertedCount = synchronizationResult.InsertedCount,
                UpdatedCount = synchronizationResult.UpdatedCount,
                DeactivatedCount = synchronizationResult.DeactivatedCount,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SourceTaxonomy import {ImportId} failed for SourceTaxonomy {SourceTaxonomyId}", importId, sourceTaxonomyId);

            await _importStore.MarkFailedAsync(importId, SanitizeErrorMessage(ex), DateTime.UtcNow, cancellationToken);

            throw;
        }
    }

    private ISourceTaxonomyAdapter ResolveAdapter(string adapterCode)
    {
        var matches = _adapters
            .Where(a => string.Equals(a.AdapterCode, adapterCode, StringComparison.Ordinal))
            .ToArray();

        return matches.Length switch
        {
            0 => throw new SourceTaxonomyAdapterNotFoundException(adapterCode),
            1 => matches[0],
            _ => throw new SourceTaxonomyDuplicateAdapterException(adapterCode)
        };
    }

    private static void ValidateSourceIdentity(
        SourceTaxonomyDescriptorRecord existing,
        SourceTaxonomySnapshotDescriptor snapshotDescriptor)
    {
        if (!string.Equals(existing.ProviderCode, snapshotDescriptor.ProviderCode, StringComparison.Ordinal))
        {
            throw new SourceTaxonomyProviderMismatchException(existing.ProviderCode, snapshotDescriptor.ProviderCode);
        }

        if (existing.ScopeCode is not null
            && snapshotDescriptor.ScopeCode is not null
            && !string.Equals(existing.ScopeCode, snapshotDescriptor.ScopeCode, StringComparison.Ordinal))
        {
            throw new SourceTaxonomyScopeConflictException(existing.ScopeCode, snapshotDescriptor.ScopeCode);
        }

        if (existing.ExternalTaxonomyId is not null
            && snapshotDescriptor.ExternalTaxonomyId is not null
            && !string.Equals(existing.ExternalTaxonomyId, snapshotDescriptor.ExternalTaxonomyId, StringComparison.Ordinal))
        {
            throw new SourceTaxonomyExternalTaxonomyIdConflictException(existing.ExternalTaxonomyId, snapshotDescriptor.ExternalTaxonomyId);
        }
    }

    private static string SanitizeErrorMessage(Exception ex)
    {
        const int maxLength = 2000;
        var message = ex.Message;
        return message.Length > maxLength ? message[..maxLength] : message;
    }
}
