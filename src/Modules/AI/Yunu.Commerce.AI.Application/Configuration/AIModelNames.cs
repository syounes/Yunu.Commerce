namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Well-known logical AI model names shared between Infrastructure adapters
/// and DI registration (docs task: "Intent/Query Rewriting"), avoiding magic
/// strings duplicated across the module. Actual endpoint/deployment/dimensions
/// for each name are supplied via configuration ("AI:Models:{Name}") and
/// resolved through <see cref="IAIModelCatalog"/>.
/// </summary>
public static class AIModelNames
{
    public const string CategoryEmbedding = "CategoryEmbedding";

    public const string IntentRewriter = "IntentRewriter";

    /// <summary>
    /// Logical Chat model used for contextual candidate reranking (docs task:
    /// "Contextual candidate reranking"). In this lab version it shares the
    /// same underlying deployment as <see cref="IntentRewriter"/>, but is
    /// registered as its own logical model so it can later be repointed at a
    /// dedicated model, a cross-encoder or another provider without touching
    /// catalog resolvers.
    /// </summary>
    public const string CatalogReranker = "CatalogReranker";
}
