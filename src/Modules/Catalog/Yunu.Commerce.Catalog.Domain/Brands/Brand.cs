using System.Globalization;
using System.Text;
using Yunu.Commerce.Catalog.Domain.Brands.Events;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Brands;

public sealed class Brand
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public BrandId Id { get; }

    public BrandCode Code { get; }

    public BrandName Name { get; private set; }

    public string NormalizedName { get; }

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

    /// <summary>
    /// Reconstitute an existing Brand from persistence without raising domain events.
    /// </summary>
    public static Brand Reconstitute(BrandId id, BrandCode code, BrandName name, string normalizedName, BrandStatus status, DateTimeOffset createdAtUtc)
    {
        return new Brand(id, code, name, normalizedName, status, createdAtUtc);
    }

    public static Brand Create(BrandId id, BrandCode code, BrandName name, BrandStatus status = BrandStatus.Active)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        var normalized = NormalizeName(name.Value);

        var brand = new Brand(id, code, name, normalized, status, DateTimeOffset.UtcNow);

        brand._domainEvents.Add(new BrandCreatedDomainEvent(id));

        return brand;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        var noDiacritics = RemoveDiacritics(trimmed);
        var normalizedWhitespace = System.Text.RegularExpressions.Regex.Replace(noDiacritics, "\\s+", " ");
        return normalizedWhitespace.ToUpperInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
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
