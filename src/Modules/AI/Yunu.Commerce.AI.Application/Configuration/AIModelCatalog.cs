using Microsoft.Extensions.Options;

namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Default <see cref="IAIModelCatalog"/> implementation backed by <see
/// cref="AIOptions"/> (docs task: "Intent/Query Rewriting"). Configuration
/// shape is validated at startup by <see cref="AIOptionsValidator"/>, so
/// resolution failures here indicate the requested model name itself is wrong
/// (e.g. a typo in code) rather than a configuration mistake.
/// </summary>
public sealed class AIModelCatalog : IAIModelCatalog
{
    private readonly AIOptions _options;

    public AIModelCatalog(IOptions<AIOptions> options)
    {
        _options = options.Value;
    }

    public ResolvedAIModel Resolve(string modelName, AIModelType expectedType)
    {
        if (!_options.Models.TryGetValue(modelName, out var model))
        {
            throw new AIModelResolutionException($"AI model '{modelName}' is not registered under \"AI:Models\".");
        }

        if (model.ModelType != expectedType)
        {
            throw new AIModelResolutionException(
                $"AI model '{modelName}' is registered as {model.ModelType} but was requested as {expectedType}.");
        }

        if (!_options.Connections.TryGetValue(model.Connection, out var connection))
        {
            throw new AIModelResolutionException(
                $"AI model '{modelName}' references connection '{model.Connection}' which is not registered under \"AI:Connections\".");
        }

        return new ResolvedAIModel(
            modelName,
            model.Connection,
            connection.Endpoint,
            connection.ApiKey,
            model.DeploymentName,
            model.ModelType,
            model.Dimensions);
    }
}
