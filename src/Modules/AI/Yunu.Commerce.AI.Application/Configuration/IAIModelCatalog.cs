namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Resolves a logical AI model registration (docs task: "Intent/Query
/// Rewriting") into its effective endpoint, credential, deployment name and
/// declared capability. This is the only configuration-facing abstraction
/// Infrastructure adapters depend on; it keeps them free of raw <see
/// cref="AIOptions"/> dictionary lookups.
/// </summary>
public interface IAIModelCatalog
{
    /// <summary>
    /// Resolves <paramref name="modelName"/> and verifies it is registered for
    /// <paramref name="expectedType"/>.
    /// </summary>
    /// <exception cref="AIModelResolutionException">
    /// The model, or the connection it points at, is not registered, or the
    /// model is registered for a different <see cref="AIModelType"/>.
    /// </exception>
    ResolvedAIModel Resolve(string modelName, AIModelType expectedType);
}
