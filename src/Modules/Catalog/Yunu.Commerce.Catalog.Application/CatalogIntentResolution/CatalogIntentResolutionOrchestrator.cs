using Microsoft.Extensions.Logging;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

/// <summary>
/// Default <see cref="ICatalogIntentResolutionOrchestrator"/> implementation
/// (docs task: "Catalog intent resolution orchestration"). Calls <see
/// cref="IIntentRewriter"/> exactly once; category and attribute hint
/// resolution then run deterministically against embeddings/pgvector/SQL
/// Server, but <see cref="IGoogleCategoryResolver"/> and <see
/// cref="IAttributeHintResolver"/> may each additionally invoke <see
/// cref="Yunu.Commerce.AI.Application.Reranking.ICandidateReranker"/>
/// (conditionally, only for candidates without an exact match, when
/// reranking is enabled) — one call for category resolution, and up to two
/// calls per attribute hint (definition and, for Enum attributes, option).
/// The orchestrator itself never calls the Intent Rewriter more than once,
/// but the overall pipeline may therefore perform additional LLM calls
/// beyond it. Never persists anything.
/// </summary>
public sealed class CatalogIntentResolutionOrchestrator : ICatalogIntentResolutionOrchestrator
{
    private readonly IIntentRewriter _intentRewriter;
    private readonly IGoogleCategoryResolver _categoryResolver;
    private readonly IAttributeHintResolver _attributeHintResolver;
    private readonly ILogger<CatalogIntentResolutionOrchestrator> _logger;

    public CatalogIntentResolutionOrchestrator(
        IIntentRewriter intentRewriter,
        IGoogleCategoryResolver categoryResolver,
        IAttributeHintResolver attributeHintResolver,
        ILogger<CatalogIntentResolutionOrchestrator> logger)
    {
        _intentRewriter = intentRewriter;
        _categoryResolver = categoryResolver;
        _attributeHintResolver = attributeHintResolver;
        _logger = logger;
    }

    public async Task<CatalogIntentResolutionResult> ResolveAsync(
        CatalogIntentResolutionRequest request,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return new CatalogIntentResolutionResult(
                CatalogIntentResolutionStatus.Invalid,
                Intent: null,
                Category: null,
                Attributes: null,
                ReadyForProposal: false,
                Warnings: ["Input cannot be null, empty or whitespace."]);
        }

        // Intent Rewriter is called exactly once; category and attribute
        // resolution reuse categoryHint/semanticQuery/attributeHints from
        // this single response. Note: the resolvers invoked below may still
        // trigger additional, conditional LLM calls via ICandidateReranker
        // (see class-level remarks) — that is unrelated to the Intent
        // Rewriter call being singular.
        var intentStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var intent = await _intentRewriter.RewriteAsync(
            new IntentRewriteRequest(request.Input, request.Locale),
            cancellationToken);
        intentStopwatch.Stop();

        ResolveGoogleCategoryResult category;

        if (string.IsNullOrWhiteSpace(intent.CategoryHint))
        {
            category = new ResolveGoogleCategoryResult(
                intent.CategoryHint ?? string.Empty,
                GoogleCategoryResolutionStatus.NotFound,
                null, null, null, null, null,
                [],
                "The Intent Rewriter did not produce a category hint.");
        }
        else
        {
            var categoryStopwatch = System.Diagnostics.Stopwatch.StartNew();

            category = await _categoryResolver.ResolveAsync(
                new ResolveGoogleCategoryRequest(
                    intent.CategoryHint,
                    intent.SemanticQuery,
                    intent.TargetLocale,
                    intent.CategorySearchQuery,
                    intent.OriginalInput,
                    intent.NormalizedQuery,
                    intent.AttributeHints),
                cancellationToken);

            categoryStopwatch.Stop();

            _logger.LogInformation(
                "Catalog intent category resolution completed in {CategoryMs}ms with status {Status}",
                categoryStopwatch.ElapsedMilliseconds,
                category.Status);
        }

        long? googleCategoryId = category.Status == GoogleCategoryResolutionStatus.Resolved
            ? category.GoogleCategoryId
            : null;

        ResolveAttributeHintsResult? attributes = null;

        if (intent.AttributeHints.Count > 0)
        {
            var attributesStopwatch = System.Diagnostics.Stopwatch.StartNew();

            attributes = await _attributeHintResolver.ResolveAsync(
                new ResolveAttributeHintsRequest(intent.AttributeHints, googleCategoryId, intent.TargetLocale),
                cancellationToken);

            attributesStopwatch.Stop();

            _logger.LogInformation(
                "Catalog intent attribute resolution completed in {AttributesMs}ms. AllResolved={AllResolved}",
                attributesStopwatch.ElapsedMilliseconds,
                attributes.AllResolved);
        }

        if (category.Status == GoogleCategoryResolutionStatus.Ambiguous)
        {
            warnings.Add("Category hint is ambiguous and requires user clarification.");
        }
        else if (category.Status == GoogleCategoryResolutionStatus.NotFound)
        {
            warnings.Add("Category hint could not be resolved to an official Google category.");
        }

        if (attributes is not null)
        {
            foreach (var attribute in attributes.Attributes)
            {
                if (attribute.Status != AttributeResolutionStatus.Resolved)
                {
                    warnings.Add($"Attribute hint '{attribute.RawName}' was not fully resolved ({attribute.Status}).");
                }
            }
        }

        var categoryResolved = category.Status == GoogleCategoryResolutionStatus.Resolved && googleCategoryId is not null;
        var attributesResolved = attributes is null || attributes.AllResolved;

        var readyForProposal =
            intent.Intent == CatalogIntent.ProductCreation &&
            categoryResolved &&
            attributesResolved;

        var status = DetermineStatus(intent, category, attributes, categoryResolved, attributesResolved);

        totalStopwatch.Stop();

        _logger.LogInformation(
            "Catalog intent resolution completed. Status={Status} ReadyForProposal={ReadyForProposal} " +
            "TotalMs={TotalMs} IntentMs={IntentMs}",
            status,
            readyForProposal,
            totalStopwatch.ElapsedMilliseconds,
            intentStopwatch.ElapsedMilliseconds);

        return new CatalogIntentResolutionResult(
            status,
            intent,
            category,
            attributes,
            readyForProposal,
            warnings);
    }

    private static CatalogIntentResolutionStatus DetermineStatus(
        IntentRewriteResult intent,
        ResolveGoogleCategoryResult category,
        ResolveAttributeHintsResult? attributes,
        bool categoryResolved,
        bool attributesResolved)
    {
        if (intent.Intent == CatalogIntent.Unknown)
        {
            return CatalogIntentResolutionStatus.NotFound;
        }

        var anyAmbiguous =
            category.Status == GoogleCategoryResolutionStatus.Ambiguous ||
            (attributes?.Attributes.Any(a => a.Status == AttributeResolutionStatus.Ambiguous) ?? false);

        if (anyAmbiguous)
        {
            return CatalogIntentResolutionStatus.NeedsClarification;
        }

        if (categoryResolved && attributesResolved)
        {
            return CatalogIntentResolutionStatus.Resolved;
        }

        if (category.Status == GoogleCategoryResolutionStatus.NotFound && !categoryResolved)
        {
            // Attributes may still be partially resolved without a category;
            // this is a clarification opportunity rather than a hard failure.
            return CatalogIntentResolutionStatus.NeedsClarification;
        }

        return CatalogIntentResolutionStatus.NeedsClarification;
    }
}
