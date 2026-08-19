using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Canonical Taxonomy Node Aggregate Root (docs task: "Canonical Taxonomy +
/// Segments Domain" §5-§6). Represents a single node of the Yunu canonical
/// classification tree, backed by SQL Server (Catalog.CanonicalTaxonomyNodes,
/// deploy/databases/sqlserver/009-reset-canonical-taxonomy-starter.sql).
///
/// Depth and Path are not caller-supplied: Catalog.Application computes them
/// from the parent node before calling <see cref="CreateChild"/> (or
/// <see cref="CreateRoot"/> for a root node), so they always stay consistent
/// with the tree. Path is built from pt-BR node names separated by " &gt; ",
/// never from Code and never using "/". IsRoot/IsLeaf/IsAssignable/
/// AppliesToDescendants are intentionally not modeled as persisted state;
/// leaf-ness in particular is derived by the Application layer from the
/// absence of children, since this Aggregate does not have visibility into
/// its descendants.
///
/// This Aggregate no longer owns a single SegmentDefinitionId: the
/// association between a node and its Segment Definitions is a many-to-many
/// relationship owned by Catalog.CanonicalTaxonomyNodeSegmentDefinitions and
/// is not modeled here.
/// </summary>
public sealed class CanonicalTaxonomyNode
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public CanonicalTaxonomyNodeId Id { get; }

    public CanonicalTaxonomyNodeId? ParentId { get; }

    public long? GoogleCategoryId { get; }

    public string Code { get; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public int Depth { get; }

    public string Path { get; }

    public CanonicalTaxonomySource Source { get; }

    public CanonicalTaxonomyNodeStatus Status { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private CanonicalTaxonomyNode(
        CanonicalTaxonomyNodeId id,
        CanonicalTaxonomyNodeId? parentId,
        long? googleCategoryId,
        string code,
        string name,
        string normalizedName,
        string? description,
        int depth,
        string path,
        CanonicalTaxonomySource source,
        CanonicalTaxonomyNodeStatus status)
    {
        Id = id;
        ParentId = parentId;
        GoogleCategoryId = googleCategoryId;
        Code = code;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        Depth = depth;
        Path = path;
        Source = source;
        Status = status;
    }

    /// <summary>
    /// Creates a root node (no parent, Depth = 0).
    /// </summary>
    public static CanonicalTaxonomyNode CreateRoot(
        CanonicalTaxonomyNodeId id,
        string code,
        string name,
        string normalizedName,
        string? description,
        string path,
        long? googleCategoryId = null,
        CanonicalTaxonomySource source = CanonicalTaxonomySource.Yunu,
        CanonicalTaxonomyNodeStatus status = CanonicalTaxonomyNodeStatus.Draft)
    {
        ValidateCommon(code, name, normalizedName, path);
        ValidateGoogleSource(source, googleCategoryId);

        var node = new CanonicalTaxonomyNode(
            id, null, googleCategoryId, code.Trim(), name.Trim(), normalizedName.Trim(),
            description, 0, path.Trim(), source, status);

        node._domainEvents.Add(new Events.CanonicalTaxonomyNodeCreatedDomainEvent(id));

        return node;
    }

    /// <summary>
    /// Creates a child node under the given parent. Depth/Path must already
    /// be computed by the Application layer from the parent's own Depth/Path
    /// (docs task §6); this Aggregate only validates their internal
    /// consistency, not the parent's actual current state.
    /// </summary>
    public static CanonicalTaxonomyNode CreateChild(
        CanonicalTaxonomyNodeId id,
        CanonicalTaxonomyNodeId parentId,
        string code,
        string name,
        string normalizedName,
        string? description,
        int depth,
        string path,
        long? googleCategoryId = null,
        CanonicalTaxonomySource source = CanonicalTaxonomySource.Yunu,
        CanonicalTaxonomyNodeStatus status = CanonicalTaxonomyNodeStatus.Draft)
    {
        ValidateCommon(code, name, normalizedName, path);
        ValidateGoogleSource(source, googleCategoryId);

        if (parentId == id)
        {
            throw new ArgumentException("A CanonicalTaxonomyNode cannot be its own parent.", nameof(parentId));
        }

        if (depth <= 0)
        {
            throw new ArgumentException("A child node's Depth must be greater than zero.", nameof(depth));
        }

        var node = new CanonicalTaxonomyNode(
            id, parentId, googleCategoryId, code.Trim(), name.Trim(), normalizedName.Trim(),
            description, depth, path.Trim(), source, status);

        node._domainEvents.Add(new Events.CanonicalTaxonomyNodeCreatedDomainEvent(id));

        return node;
    }

    /// <summary>
    /// Rehydrates a CanonicalTaxonomyNode from persisted state without
    /// raising creation-time domain events. Used exclusively by
    /// Infrastructure persistence adapters.
    /// </summary>
    public static CanonicalTaxonomyNode Hydrate(
        CanonicalTaxonomyNodeId id,
        CanonicalTaxonomyNodeId? parentId,
        long? googleCategoryId,
        string code,
        string name,
        string normalizedName,
        string? description,
        int depth,
        string path,
        CanonicalTaxonomySource source,
        CanonicalTaxonomyNodeStatus status)
    {
        return new CanonicalTaxonomyNode(
            id, parentId, googleCategoryId, code, name, normalizedName,
            description, depth, path, source, status);
    }

    /// <summary>
    /// Renames a leaf node (Name/NormalizedName/Description). Leaf-ness and
    /// usage-by-Product checks are enforced by Catalog.Application before
    /// calling this method (docs task §22); the Aggregate itself has no
    /// visibility into children or Products.
    /// </summary>
    public void Update(string name, string normalizedName, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null, empty or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("NormalizedName cannot be null, empty or whitespace.", nameof(normalizedName));
        }

        Name = name.Trim();
        NormalizedName = normalizedName.Trim();
        Description = description;

        _domainEvents.Add(new Events.CanonicalTaxonomyNodeUpdatedDomainEvent(Id));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private static void ValidateCommon(string code, string name, string normalizedName, string path)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code cannot be null, empty or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null, empty or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("NormalizedName cannot be null, empty or whitespace.", nameof(normalizedName));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null, empty or whitespace.", nameof(path));
        }
    }

    private static void ValidateGoogleSource(CanonicalTaxonomySource source, long? googleCategoryId)
    {
        if (source == CanonicalTaxonomySource.Google && googleCategoryId is null)
        {
            throw new ArgumentException("A node with Source = Google must have a GoogleCategoryId.", nameof(googleCategoryId));
        }
    }
}
