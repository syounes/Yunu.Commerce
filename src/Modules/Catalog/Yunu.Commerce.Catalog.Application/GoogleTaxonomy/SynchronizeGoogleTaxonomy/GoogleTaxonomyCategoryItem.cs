namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

/// <summary>
/// Parsed representation of a single Google Product Taxonomy row
/// (docs/domains/catalog.md - external classification systems). This model is
/// intentionally independent from the Yunu internal Department/Category/SubCategory/
/// Family taxonomy: Google taxonomy is an external classification system that
/// coexists with, but never replaces, the Yunu canonical taxonomy.
/// </summary>
public sealed record GoogleTaxonomyCategoryItem(
    int GoogleCategoryId,
    int? ParentGoogleCategoryId,
    string Name,
    string FullPath,
    int Level,
    bool IsLeaf);
