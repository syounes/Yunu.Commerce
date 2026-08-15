namespace Yunu.Commerce.Catalog.Domain.Attributes;

/// <summary>
/// Provenance of a Sku attribute assignment, mirroring the CHECK constraint on
/// SQL Server Catalog.SkuAttributeValues.Source
/// (deploy/sql/002_create_sku_attribute_catalog.sql). This task only assigns
/// attributes explicitly supplied by a caller (<see cref="User"/> or
/// <see cref="Import"/>); AI/Google-sourced attribute interpretation is
/// deferred (docs task: "SKU attribute foundation" - out of scope: AI, LLM,
/// embeddings, automatic extraction).
/// </summary>
public enum SkuAttributeSource
{
    User,
    Import,
    AI,
    Google,
    System
}
