namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// A pgvector semantic search hit for an Attribute Definition
/// (public.sku_attribute_embeddings, entity_type = 'AttributeDefinition').
/// <see cref="AttributeCode"/> is the integration identity used by the
/// synchronizer (<see cref="Yunu.Commerce.Catalog.Application.AttributeEmbeddings.AttributeSemanticDocumentBuilder.BuildDefinitionEntityId"/>
/// returns the attribute code itself), never a numeric SQL Server id: the
/// candidate must still be hydrated and validated against SQL Server before
/// being trusted.
/// </summary>
public sealed record SemanticAttributeCandidate(
    string AttributeCode,
    string Name,
    double Similarity);

/// <summary>
/// A pgvector semantic search hit for an Attribute Option
/// (public.sku_attribute_embeddings, entity_type = 'AttributeOption'),
/// scoped to a single parent attribute code
/// (<see cref="Yunu.Commerce.Catalog.Application.AttributeEmbeddings.AttributeSemanticDocumentBuilder.BuildOptionEntityId"/>
/// = "{attributeCode}:{optionCode}"). Must still be hydrated and validated
/// against SQL Server, including confirming it belongs to the resolved parent
/// attribute, before being trusted.
/// </summary>
public sealed record SemanticAttributeOptionCandidate(
    string AttributeCode,
    string OptionCode,
    string Name,
    double Similarity);

/// <summary>
/// Read-only port for pgvector semantic search over SKU attribute embeddings
/// (public.sku_attribute_embeddings). Never validates existence, activity or
/// relationships: that responsibility belongs to
/// <see cref="IAttributeCatalogReader"/> (SQL Server, the source of truth).
/// Implementations must only consider rows where is_active = true,
/// embedding IS NOT NULL and embedded_content_hash = content_hash.
/// </summary>
public interface IAttributeSemanticSearch
{
    Task<IReadOnlyList<SemanticAttributeCandidate>> SearchDefinitionsAsync(
        ReadOnlyMemory<float> embedding,
        int topK,
        string locale,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticAttributeOptionCandidate>> SearchOptionsAsync(
        string attributeCode,
        ReadOnlyMemory<float> embedding,
        int topK,
        string locale,
        CancellationToken cancellationToken);
}
