using Microsoft.Extensions.Options;

namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Validates <see cref="AIOptions"/> at startup (ValidateOnStart) so a
/// misconfigured connection or model registration fails fast instead of at
/// first request (docs task: "Intent/Query Rewriting"). Never includes any
/// ApiKey value in a failure message.
/// </summary>
public sealed class AIOptionsValidator : IValidateOptions<AIOptions>
{
    public ValidateOptionsResult Validate(string? name, AIOptions options)
    {
        foreach (var (connectionName, connection) in options.Connections)
        {
            if (string.IsNullOrWhiteSpace(connection.Endpoint))
            {
                return ValidateOptionsResult.Fail(
                    $"AI:Connections:{connectionName}:Endpoint is required.");
            }

            if (!Uri.TryCreate(connection.Endpoint, UriKind.Absolute, out _))
            {
                return ValidateOptionsResult.Fail(
                    $"AI:Connections:{connectionName}:Endpoint must be a valid absolute URI.");
            }

            if (string.IsNullOrWhiteSpace(connection.ApiKey))
            {
                return ValidateOptionsResult.Fail(
                    $"AI:Connections:{connectionName}:ApiKey is required. Set it via .NET User Secrets or the " +
                    $"AI__Connections__{connectionName}__ApiKey environment variable; never in appsettings.json.");
            }
        }

        foreach (var (modelName, model) in options.Models)
        {
            if (string.IsNullOrWhiteSpace(model.Connection))
            {
                return ValidateOptionsResult.Fail($"AI:Models:{modelName}:Connection is required.");
            }

            if (!options.Connections.ContainsKey(model.Connection))
            {
                return ValidateOptionsResult.Fail(
                    $"AI:Models:{modelName}:Connection references '{model.Connection}' which is not registered under \"AI:Connections\".");
            }

            if (string.IsNullOrWhiteSpace(model.DeploymentName))
            {
                return ValidateOptionsResult.Fail($"AI:Models:{modelName}:DeploymentName is required.");
            }

            if (!Enum.IsDefined(model.ModelType))
            {
                return ValidateOptionsResult.Fail(
                    $"AI:Models:{modelName}:ModelType must be one of: {string.Join(", ", Enum.GetNames<AIModelType>())}.");
            }

            if (model.ModelType == AIModelType.Embedding && (model.Dimensions is null or <= 0))
            {
                return ValidateOptionsResult.Fail(
                    $"AI:Models:{modelName}:Dimensions must be greater than zero for Embedding models.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
