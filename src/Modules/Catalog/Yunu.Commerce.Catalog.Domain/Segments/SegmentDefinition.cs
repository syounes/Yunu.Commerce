namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Thrown when an invalid Status transition is attempted on a
/// <see cref="SegmentDefinition"/> (e.g. Archived being edited, or a
/// transition outside the explicitly allowed set).
/// </summary>
public sealed class InvalidSegmentDefinitionStatusTransitionException : Exception
{
    public InvalidSegmentDefinitionStatusTransitionException(string message) : base(message)
    {
    }
}

/// <summary>
/// Thrown when a structural field (SelectionMode, AssignmentScope) is
/// changed while the Segment Definition is not in
/// <see cref="SegmentDefinitionStatus.Draft"/>. Structural fields remain
/// Draft-only according to this Domain rule; this is a separate concern from
/// the lifecycle/usage guards involving external references (Approved
/// Canonical Taxonomy associations, Product/Sku Segment assignments), which
/// are enforced by Catalog.Application, not by this Aggregate.
/// </summary>
public sealed class SegmentDefinitionStructuralChangeNotAllowedException : Exception
{
    public SegmentDefinitionStructuralChangeNotAllowedException(string message) : base(message)
    {
    }
}

/// <summary>
/// Segment Definition Aggregate Root. Represents a reference-data
/// classification axis (e.g. gender, target_audience, sport_modality) that
/// Product/Sku can be assigned against (docs task: "Canonical Taxonomy +
/// Segments Domain"). Backed by SQL Server (Catalog.SegmentDefinitions).
///
/// The physical identity is a BIGINT IDENTITY column, so a Definition that
/// has not yet been persisted has no identity: <see cref="Id"/> is null
/// until <see cref="Create"/>'s Aggregate is assigned an identity by
/// ISegmentDefinitionRepository.AddAsync. Zero is never used as a
/// placeholder identity (see <see cref="SegmentDefinitionId"/>).
///
/// Does not carry an IsRequired flag: obligatoriness of a Segment is
/// contextual to where it is associated in the Canonical Taxonomy, not a
/// global property of the Definition itself. That contextual value lives
/// exclusively on Catalog.CanonicalTaxonomyNodeSegmentDefinitions.IsRequired
/// and is surfaced by EffectiveSegmentDefinitionResolver (docs task:
/// "Consolidar a semântica de IsRequired em Segments").
/// </summary>
public sealed class SegmentDefinition
{
    private static readonly Dictionary<SegmentDefinitionStatus, HashSet<SegmentDefinitionStatus>> AllowedTransitions = new()
    {
        [SegmentDefinitionStatus.Draft] = new HashSet<SegmentDefinitionStatus> { SegmentDefinitionStatus.Active, SegmentDefinitionStatus.Archived },
        [SegmentDefinitionStatus.Active] = new HashSet<SegmentDefinitionStatus> { SegmentDefinitionStatus.Inactive, SegmentDefinitionStatus.Archived },
        [SegmentDefinitionStatus.Inactive] = new HashSet<SegmentDefinitionStatus> { SegmentDefinitionStatus.Active, SegmentDefinitionStatus.Archived },
        [SegmentDefinitionStatus.Archived] = new HashSet<SegmentDefinitionStatus>()
    };

    public const int DescriptionMaxLength = 1000;
    public const int SemanticTextMaxLength = 2000;

    public SegmentDefinitionId? Id { get; private set; }

    public SegmentDefinitionCode Code { get; }

    public SegmentDefinitionName Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public string? SemanticText { get; private set; }

    public SegmentSelectionMode SelectionMode { get; private set; }

    public SegmentAssignmentScope AssignmentScope { get; private set; }

    public SegmentDefinitionStatus Status { get; private set; }

    private SegmentDefinition(
        SegmentDefinitionId? id,
        SegmentDefinitionCode code,
        SegmentDefinitionName name,
        string normalizedName,
        string? description,
        string? semanticText,
        SegmentSelectionMode selectionMode,
        SegmentAssignmentScope assignmentScope,
        SegmentDefinitionStatus status)
    {
        Id = id;
        Code = code;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        SemanticText = semanticText;
        SelectionMode = selectionMode;
        AssignmentScope = assignmentScope;
        Status = status;
    }

    /// <summary>
    /// Creates a not-yet-persisted Segment Definition. Always starts as
    /// <see cref="SegmentDefinitionStatus.Draft"/>; the caller cannot choose
    /// another initial status.
    /// </summary>
    public static SegmentDefinition Create(
        SegmentDefinitionCode code,
        SegmentDefinitionName name,
        string? description,
        string? semanticText,
        SegmentSelectionMode selectionMode,
        SegmentAssignmentScope assignmentScope)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        var normalizedName = SegmentTextNormalizer.Normalize(name.Value);
        var normalizedDescription = NormalizeOptionalText(description, DescriptionMaxLength, nameof(description));
        var normalizedSemanticText = NormalizeOptionalText(semanticText, SemanticTextMaxLength, nameof(semanticText));

        return new SegmentDefinition(
            null,
            code,
            name,
            normalizedName,
            normalizedDescription,
            normalizedSemanticText,
            selectionMode,
            assignmentScope,
            SegmentDefinitionStatus.Draft);
    }

    /// <summary>
    /// Reconstitutes an existing Segment Definition from persistence without
    /// executing transitions or raising creation-time behavior. Requires a
    /// valid (persisted) <see cref="SegmentDefinitionId"/>.
    /// </summary>
    public static SegmentDefinition Hydrate(
        SegmentDefinitionId id,
        SegmentDefinitionCode code,
        SegmentDefinitionName name,
        string normalizedName,
        string? description,
        string? semanticText,
        SegmentSelectionMode selectionMode,
        SegmentAssignmentScope assignmentScope,
        SegmentDefinitionStatus status)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        return new SegmentDefinition(
            id,
            code,
            name,
            normalizedName,
            description,
            semanticText,
            selectionMode,
            assignmentScope,
            status);
    }

    /// <summary>
    /// Assigns the identity generated by SQL Server after a successful
    /// insert. Used exclusively by Infrastructure persistence adapters.
    /// </summary>
    public void AssignIdentity(SegmentDefinitionId id)
    {
        if (Id is not null)
        {
            throw new InvalidOperationException("SegmentDefinition already has an identity assigned.");
        }

        Id = id;
    }

    /// <summary>
    /// Full update of the mutable fields. Code is never accepted here: it is
    /// immutable after creation. Structural fields (SelectionMode,
    /// AssignmentScope) can only change while the Definition is Draft;
    /// outside Draft, keeping the same structural values is allowed, but
    /// changing them throws. The Status transition is validated and applied
    /// only after the other fields have been validated/applied.
    /// </summary>
    public void Update(
        SegmentDefinitionName name,
        string? description,
        string? semanticText,
        SegmentSelectionMode selectionMode,
        SegmentAssignmentScope assignmentScope,
        SegmentDefinitionStatus status)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Status == SegmentDefinitionStatus.Archived)
        {
            throw new SegmentDefinitionStructuralChangeNotAllowedException(
                "An Archived SegmentDefinition cannot be edited.");
        }

        var structuralChanged =
            selectionMode != SelectionMode ||
            assignmentScope != AssignmentScope;

        if (structuralChanged && Status != SegmentDefinitionStatus.Draft)
        {
            throw new SegmentDefinitionStructuralChangeNotAllowedException(
                "Structural fields (SelectionMode, AssignmentScope) can only be changed while the SegmentDefinition is Draft.");
        }

        var normalizedDescription = NormalizeOptionalText(description, DescriptionMaxLength, nameof(description));
        var normalizedSemanticText = NormalizeOptionalText(semanticText, SemanticTextMaxLength, nameof(semanticText));

        Name = name;
        NormalizedName = SegmentTextNormalizer.Normalize(name.Value);
        Description = normalizedDescription;
        SemanticText = normalizedSemanticText;
        SelectionMode = selectionMode;
        AssignmentScope = assignmentScope;

        TransitionTo(status);
    }

    private void TransitionTo(SegmentDefinitionStatus newStatus)
    {
        if (newStatus == Status)
        {
            return;
        }

        if (!AllowedTransitions[Status].Contains(newStatus))
        {
            throw new InvalidSegmentDefinitionStatusTransitionException(
                $"Cannot transition SegmentDefinition status from {Status} to {newStatus}.");
        }

        Status = newStatus;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);
        }

        return trimmed;
    }
}
