namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Required external classification for a Product, resolved from the canonical
/// Google Product Taxonomy (SQL Server GoogleTaxonomyCategories, owned by
/// Catalog.Infrastructure) before Product creation. The Application layer
/// resolves and validates the Google category id/path; Product only stores
/// this denormalized reference and never performs the lookup itself
/// (docs/domains/catalog.md - external classification systems).
/// </summary>
public sealed record GoogleCategoryReference
{
    public int Id { get; }

    public string Path { get; }

    public GoogleCategoryReference(int id, string path)
    {
        if (id <= 0)
        {
            throw new ArgumentException("GoogleCategoryReference id must be greater than zero.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("GoogleCategoryReference path cannot be null, empty or whitespace.", nameof(path));
        }

        Id = id;
        Path = path;
    }

    public override string ToString() => Path;
}
