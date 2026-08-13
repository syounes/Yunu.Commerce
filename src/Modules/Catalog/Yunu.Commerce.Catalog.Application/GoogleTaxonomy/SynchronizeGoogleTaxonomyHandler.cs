using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Orchestrates a full Google Product Taxonomy synchronization:
/// download → parse → build hierarchy → validate → persist (docs task:
/// "Implement the complete Google Product Taxonomy import/synchronization feature").
///
/// Business orchestration only. Downloading is delegated to
/// <see cref="IGoogleTaxonomySource"/>, parsing to <see cref="GoogleTaxonomyParser"/>,
/// hierarchy reconstruction/validation to <see cref="GoogleTaxonomyHierarchyBuilder"/>,
/// and persistence to <see cref="IGoogleTaxonomyRepository"/>. Concurrent
/// synchronizations are rejected via <see cref="IGoogleTaxonomySynchronizationGuard"/>.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyHandler
{
    private readonly IGoogleTaxonomySource _taxonomySource;
    private readonly IGoogleTaxonomyRepository _taxonomyRepository;
    private readonly IGoogleTaxonomySynchronizationGuard _synchronizationGuard;
    private readonly GoogleTaxonomyOptions _options;
    private readonly ILogger<SynchronizeGoogleTaxonomyHandler> _logger;

    public SynchronizeGoogleTaxonomyHandler(
        IGoogleTaxonomySource taxonomySource,
        IGoogleTaxonomyRepository taxonomyRepository,
        IGoogleTaxonomySynchronizationGuard synchronizationGuard,
        IOptions<GoogleTaxonomyOptions> options,
        ILogger<SynchronizeGoogleTaxonomyHandler> logger)
    {
        _taxonomySource = taxonomySource;
        _taxonomyRepository = taxonomyRepository;
        _synchronizationGuard = synchronizationGuard;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SynchronizeGoogleTaxonomyResult> HandleAsync(
        SynchronizeGoogleTaxonomyCommand command,
        CancellationToken cancellationToken)
    {
        using var lockToken = _synchronizationGuard.TryAcquire();

        if (lockToken is null)
        {
            throw new GoogleTaxonomySynchronizationInProgressException();
        }

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Google taxonomy synchronization started from {SourceUrl}", _options.SourceUrl);

        var rawLines = await _taxonomySource.GetTaxonomyAsync(cancellationToken);

        _logger.LogInformation("Google taxonomy downloaded with {LineCount} raw lines", rawLines.Count);

        var parsedRows = GoogleTaxonomyParser.Parse(rawLines);

        _logger.LogInformation("Google taxonomy parsed into {RowCount} valid rows", parsedRows.Count);

        var categories = GoogleTaxonomyHierarchyBuilder.Build(parsedRows);

        _logger.LogInformation("Google taxonomy validated with {CategoryCount} categories", categories.Count);

        var synchronizationResult = await _taxonomyRepository.SynchronizeAsync(
            categories,
            _options.Language,
            _options.SourceUrl,
            startedAtUtc,
            cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Google taxonomy synchronization completed in {DurationMs}ms. Inserted={Inserted} Updated={Updated} Deactivated={Deactivated}",
            stopwatch.ElapsedMilliseconds,
            synchronizationResult.Inserted,
            synchronizationResult.Updated,
            synchronizationResult.Deactivated);

        return new SynchronizeGoogleTaxonomyResult
        {
            Status = "Completed",
            TotalCategories = synchronizationResult.TotalCategories,
            Inserted = synchronizationResult.Inserted,
            Updated = synchronizationResult.Updated,
            Deactivated = synchronizationResult.Deactivated,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow
        };
    }
}
