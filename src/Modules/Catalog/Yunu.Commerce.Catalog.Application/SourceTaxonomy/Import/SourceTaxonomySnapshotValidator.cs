namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Provider-neutral structural validation for a normalized
/// <see cref="SourceTaxonomySnapshot"/>, executed before any catalog
/// mutation (docs/adr/0014-provider-neutral-source-taxonomy.md §7). Multiple
/// roots are valid by design; no CanonicalTaxonomyRootTopologyPolicy
/// (docs/adr/0013) is applied to SourceTaxonomy import. An empty snapshot is
/// rejected (ADR-0014 §6) to protect against catastrophic mass
/// deactivation caused by an upstream provider failure returning zero
/// nodes.
/// </summary>
public static class SourceTaxonomySnapshotValidator
{
    public static void Validate(SourceTaxonomySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            throw new SourceTaxonomySnapshotValidationException("The source taxonomy snapshot must not be null.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Descriptor.ProviderCode))
        {
            throw new SourceTaxonomySnapshotValidationException("The snapshot ProviderCode must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Descriptor.Locale))
        {
            throw new SourceTaxonomySnapshotValidationException("The snapshot Locale must not be blank.");
        }

        if (snapshot.Nodes is null)
        {
            throw new SourceTaxonomySnapshotValidationException("The snapshot node collection must not be null.");
        }

        if (snapshot.Nodes.Count == 0)
        {
            throw new SourceTaxonomySnapshotValidationException(
                "The snapshot node collection must not be empty. An empty snapshot is rejected to avoid mass-deactivating an existing source taxonomy.");
        }

        var nodesByExternalId = new Dictionary<string, SourceTaxonomySnapshotNode>(StringComparer.Ordinal);

        foreach (var node in snapshot.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.ExternalNodeId))
            {
                throw new SourceTaxonomySnapshotValidationException("Every snapshot node must have a non-blank ExternalNodeId.");
            }

            if (string.IsNullOrWhiteSpace(node.NodeType))
            {
                throw new SourceTaxonomySnapshotValidationException($"Node '{node.ExternalNodeId}' must have a non-blank NodeType.");
            }

            if (string.IsNullOrWhiteSpace(node.Name))
            {
                throw new SourceTaxonomySnapshotValidationException($"Node '{node.ExternalNodeId}' must have a non-blank Name.");
            }

            if (string.IsNullOrWhiteSpace(node.FullPath))
            {
                throw new SourceTaxonomySnapshotValidationException($"Node '{node.ExternalNodeId}' must have a non-blank FullPath.");
            }

            if (node.Level < 0)
            {
                throw new SourceTaxonomySnapshotValidationException($"Node '{node.ExternalNodeId}' must not have a negative Level.");
            }

            if (!nodesByExternalId.TryAdd(node.ExternalNodeId, node))
            {
                throw new SourceTaxonomySnapshotValidationException($"Duplicate ExternalNodeId '{node.ExternalNodeId}' found in the snapshot.");
            }
        }

        foreach (var node in snapshot.Nodes)
        {
            if (node.ParentExternalNodeId is null)
            {
                continue;
            }

            if (string.Equals(node.ParentExternalNodeId, node.ExternalNodeId, StringComparison.Ordinal))
            {
                throw new SourceTaxonomySnapshotValidationException($"Node '{node.ExternalNodeId}' references itself as its own parent.");
            }

            if (!nodesByExternalId.ContainsKey(node.ParentExternalNodeId))
            {
                throw new SourceTaxonomySnapshotValidationException(
                    $"Node '{node.ExternalNodeId}' references parent '{node.ParentExternalNodeId}' that does not exist in the snapshot.");
            }
        }

        DetectCycles(nodesByExternalId);
    }

    private static void DetectCycles(Dictionary<string, SourceTaxonomySnapshotNode> nodesByExternalId)
    {
        var visitState = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var externalNodeId in nodesByExternalId.Keys)
        {
            VisitForCycleDetection(externalNodeId, nodesByExternalId, visitState);
        }
    }

    private static void VisitForCycleDetection(
        string externalNodeId,
        Dictionary<string, SourceTaxonomySnapshotNode> nodesByExternalId,
        Dictionary<string, int> visitState)
    {
        if (visitState.TryGetValue(externalNodeId, out var state))
        {
            if (state == 1)
            {
                throw new SourceTaxonomySnapshotValidationException(
                    $"A cycle was detected in the snapshot hierarchy involving node '{externalNodeId}'.");
            }

            return;
        }

        visitState[externalNodeId] = 1;

        var parentExternalNodeId = nodesByExternalId[externalNodeId].ParentExternalNodeId;

        if (parentExternalNodeId is not null)
        {
            VisitForCycleDetection(parentExternalNodeId, nodesByExternalId, visitState);
        }

        visitState[externalNodeId] = 2;
    }
}
