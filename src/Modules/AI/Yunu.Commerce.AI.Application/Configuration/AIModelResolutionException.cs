namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Raised when a logical AI model name cannot be resolved: it is not
/// registered, points at a connection that does not exist, or does not match
/// the requested capability (docs task: "Intent/Query Rewriting"). This
/// indicates a configuration error and should normally only be observed at
/// startup (options are validated with ValidateOnStart).
/// </summary>
public sealed class AIModelResolutionException : Exception
{
    public AIModelResolutionException(string message) : base(message)
    {
    }
}
