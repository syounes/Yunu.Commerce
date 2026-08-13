namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Dedicated read model for a persisted Google Taxonomy category, returned by
/// query endpoints. Decoupled from any SQL Server / vendor-specific row shape.
/// </summary>
public sealed class GoogleTaxonomyCategoryResponse
{
    public required int GoogleCategoryId { get; init; }

    public int? ParentGoogleCategoryId { get; init; }

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required int Level { get; init; }

    public required bool IsLeaf { get; init; }

    public required bool IsActive { get; init; }
}
