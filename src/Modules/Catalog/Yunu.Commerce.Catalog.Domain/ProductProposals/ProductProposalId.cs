namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Strongly typed, database-independent identity for a <see cref="ProductProposal"/>
/// (docs task: "Catalog intent resolution orchestration" - proposal persistence).
/// </summary>
public readonly record struct ProductProposalId
{
    public Guid Value { get; }

    public ProductProposalId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ProductProposalId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static ProductProposalId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
