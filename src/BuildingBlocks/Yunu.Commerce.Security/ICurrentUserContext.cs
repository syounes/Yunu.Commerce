namespace Yunu.Commerce.Security;

/// <summary>
/// Technical abstraction for the current authenticated principal (docs §14).
/// Identity provider-specific implementation (e.g. Microsoft Entra ID) is an
/// outer infrastructure concern and must not leak here.
/// </summary>
public interface ICurrentUserContext
{
    string? UserId { get; }

    bool IsAuthenticated { get; }
}
