using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Yunu.Commerce.Catalog.Domain.Brands.Events;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Brands;

/// <summary>
/// Brand Aggregate Root (docs/domains/catalog.md \u00a712). Canonical master/reference
/// data owned by Catalog; Product references Brand only by <see cref="BrandId"/>.
/// </summary>
public sealed class Brand
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public BrandId Id { get; }

    public BrandCode Code { get; }

    public BrandName Name { get; private set; }

    public string NormalizedName { get; private set; }

    public BrandStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private Brand(BrandId id, BrandCode code, BrandName name, string normalizedName, BrandStatus status, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Code = code;
        Name = name;
        NormalizedName = normalizedName;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public static Brand Create(BrandId id, BrandCode code, BrandName name, BrandStatus status = BrandStatus.Active)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        var normalized = ComputeNormalizedName(name.Value);

        var brand = new Brand(id, code, name, normalized, status, DateTimeOffset.UtcNow);

        brand._domainEvents.Add(new BrandCreatedDomainEvent(id));

        return brand;
    }

    /// <summary>
    /// Reconstitute an existing Brand from persistence without raising domain events.
    /// </summary>
    public static Brand Reconstitute(BrandId id, BrandCode code, BrandName name, string normalizedName, BrandStatus status, DateTimeOffset createdAtUtc)
    {
        return new Brand(id, code, name, normalizedName, status, createdAtUtc);
    }

    /// <summary>
    /// Renames the Brand. Idempotent: renaming to the same effective name is a no-op.
    /// Keeps <see cref="Name"/> and <see cref="NormalizedName"/> consistent.
    /// </summary>
    public void Rename(BrandName newName)
    {
        ArgumentNullException.ThrowIfNull(newName);

        if (Name.Value == newName.Value) return;

        Name = newName;
        NormalizedName = ComputeNormalizedName(newName.Value);
    }

    /// <summary>
    /// Activates the Brand. Idempotent: activating an already Active Brand is a no-op.
    /// </summary>
    public void Activate()
    {
        Status = BrandStatus.Active;
    }

    /// <summary>
    /// Deactivates the Brand. Idempotent: deactivating an already Inactive Brand is a no-op.
    /// </summary>
    public void Deactivate()
    {
        Status = BrandStatus.Inactive;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Single, deterministic normalization algorithm for Brand names
    /// (docs/domains/catalog.md \u00a712): trim, remove diacritics, collapse
    /// whitespace, invariant-uppercase. Non-AI, culture-safe, consistent everywhere.
    /// </summary>
    public static string ComputeNormalizedName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        var noDiacritics = RemoveDiacritics(trimmed);
        var normalizedWhitespace = Regex.Replace(noDiacritics, "\\s+", " ");
        return normalizedWhitespace.ToUpperInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
