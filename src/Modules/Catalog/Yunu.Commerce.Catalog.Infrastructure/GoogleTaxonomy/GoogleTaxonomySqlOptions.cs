namespace Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy;

/// <summary>
/// Environment-specific SQL Server configuration for Google Taxonomy persistence
/// (docs/adr/0003-database-per-bounded-context.md §9). Contains only values
/// that genuinely vary by environment.
/// </summary>
public sealed class GoogleTaxonomySqlOptions
{
    public required string ConnectionString { get; init; }
}
