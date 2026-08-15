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
}
