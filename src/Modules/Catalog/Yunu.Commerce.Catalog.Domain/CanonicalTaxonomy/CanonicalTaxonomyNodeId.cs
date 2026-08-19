namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Strongly typed identifier referencing a Canonical Taxonomy node owned by SQL
/// Server (Catalog.CanonicalTaxonomyNodes). Catalog.Domain never queries SQL
/// Server directly; the Application layer resolves and validates the node
/// before it is referenced by a Product (docs task: "Canonical Taxonomy + Segments Domain").
///
/// Value 0 represents a not-yet-persisted node (SQL Server IDENTITY assigns
/// the real value on INSERT); Infrastructure returns the assigned id from
/// ICanonicalTaxonomyRepository.AddAsync.
/// </summary>
public readonly record struct CanonicalTaxonomyNodeId
{
    public long Value { get; }

    public CanonicalTaxonomyNodeId(long value)
    {
        if (value < 0)
        {
            throw new ArgumentException("CanonicalTaxonomyNodeId cannot be negative.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
