namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Thrown when an invalid Status transition is attempted on a
/// <see cref="SegmentOption"/> (e.g. Archived being edited, or a transition
/// outside the explicitly allowed set). Mirrors
/// <see cref="InvalidSegmentDefinitionStatusTransitionException"/>.
/// </summary>
public sealed class InvalidSegmentOptionStatusTransitionException : Exception
{
    public InvalidSegmentOptionStatusTransitionException(string message) : base(message)
    {
    }
}

/// <summary>
/// Segment Option Aggregate Root. Represents a single possible value of a
/// <see cref="SegmentDefinition"/> (e.g. MALE/FEMALE/UNISEX for the "gender"
/// Definition) that Product/Sku can be assigned against (docs task:
/// "Implementar Domain + Write-Side de SegmentOption"). Backed by SQL Server
/// (Catalog.SegmentOptions).
///
/// The physical identity is a BIGINT IDENTITY column, so an Option that has
/// not yet been persisted has no identity: <see cref="Id"/> is null until
/// <see cref="Create"/>'s Aggregate is assigned an identity by
/// ISegmentOptionRepository.AddAsync. Zero is never used as a placeholder
/// identity (see <see cref="SegmentOptionId"/>).
///
/// <see cref="SegmentDefinitionId"/> is immutable after creation: an Option
/// belongs to exactly one Definition for its entire lifetime. Moving an
/// Option to a different Definition would change its semantic identity
/// (docs task: "Implementar Domain + Write-Side de SegmentOption" - "Regra
/// fundamental"), so no operation is exposed to change it; a caller who
/// needs a different association must create a new Option instead.
/// </summary>
public sealed class SegmentOption
{
    private static readonly Dictionary<SegmentOptionStatus, HashSet<SegmentOptionStatus>> AllowedTransitions = new()
    {
        [SegmentOptionStatus.Draft] = new HashSet<SegmentOptionStatus> { SegmentOptionStatus.Active, SegmentOptionStatus.Archived },
        [SegmentOptionStatus.Active] = new HashSet<SegmentOptionStatus> { SegmentOptionStatus.Inactive, SegmentOptionStatus.Archived },
        [SegmentOptionStatus.Inactive] = new HashSet<SegmentOptionStatus> { SegmentOptionStatus.Active, SegmentOptionStatus.Archived },
        [SegmentOptionStatus.Archived] = new HashSet<SegmentOptionStatus>()
    };

    public const int DescriptionMaxLength = 1000;
    public const int SemanticTextMaxLength = 2000;

    public SegmentOptionId? Id { get; private set; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public SegmentOptionCode Code { get; }

    public SegmentOptionName Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public string? SemanticText { get; private set; }

    public int DisplayOrder { get; private set; }

    public SegmentOptionStatus Status { get; private set; }

    private SegmentOption(
        SegmentOptionId? id,
        SegmentDefinitionId segmentDefinitionId,
        SegmentOptionCode code,
        SegmentOptionName name,
        string normalizedName,
        string? description,
        string? semanticText,
        int displayOrder,
        SegmentOptionStatus status)
    {
        Id = id;
        SegmentDefinitionId = segmentDefinitionId;
        Code = code;
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        SemanticText = semanticText;
        DisplayOrder = displayOrder;
        Status = status;
    }

    /// <summary>
    /// Creates a not-yet-persisted Segment Option. Always starts as
    /// <see cref="SegmentOptionStatus.Draft"/>; the caller cannot choose
    /// another initial status. The caller (Application layer) is
    /// responsible for verifying that <paramref name="segmentDefinitionId"/>
    /// refers to an existing, appropriate SegmentDefinition; the Domain
    /// never queries SQL Server directly.
    /// </summary>
    public static SegmentOption Create(
        SegmentDefinitionId segmentDefinitionId,
        SegmentOptionCode code,
        SegmentOptionName name,
        string? description,
        string? semanticText,
        int displayOrder)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        if (displayOrder < 0)
        {
            throw new ArgumentException("DisplayOrder cannot be negative.", nameof(displayOrder));
        }

        var normalizedName = SegmentTextNormalizer.Normalize(name.Value);
        var normalizedDescription = NormalizeOptionalText(description, DescriptionMaxLength, nameof(description));
        var normalizedSemanticText = NormalizeOptionalText(semanticText, SemanticTextMaxLength, nameof(semanticText));

        return new SegmentOption(
            null,
            segmentDefinitionId,
            code,
            name,
            normalizedName,
            normalizedDescription,
            normalizedSemanticText,
            displayOrder,
            SegmentOptionStatus.Draft);
    }

    /// <summary>
    /// Reconstitutes an existing Segment Option from persistence without
    /// executing transitions or raising creation-time behavior. Requires a
    /// valid (persisted) <see cref="SegmentOptionId"/>.
    /// </summary>
    public static SegmentOption Hydrate(
        SegmentOptionId id,
        SegmentDefinitionId segmentDefinitionId,
        SegmentOptionCode code,
        SegmentOptionName name,
        string normalizedName,
        string? description,
        string? semanticText,
        int displayOrder,
        SegmentOptionStatus status)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);

        return new SegmentOption(
            id,
            segmentDefinitionId,
            code,
            name,
            normalizedName,
            description,
            semanticText,
            displayOrder,
            status);
    }

    /// <summary>
    /// Assigns the identity generated by SQL Server after a successful
    /// insert. Used exclusively by Infrastructure persistence adapters.
    /// </summary>
    public void AssignIdentity(SegmentOptionId id)
    {
        if (Id is not null)
        {
            throw new InvalidOperationException("SegmentOption already has an identity assigned.");
        }

        Id = id;
    }

    /// <summary>
    /// Full update of the mutable fields. Code and SegmentDefinitionId are
    /// never accepted here: Code is immutable after creation (mirroring
    /// SegmentDefinition), and SegmentDefinitionId can never change (see
    /// class remarks). The Status transition is validated and applied only
    /// after the other fields have been validated/applied.
    /// </summary>
    public void Update(
        SegmentOptionName name,
        string? description,
        string? semanticText,
        int displayOrder,
        SegmentOptionStatus status)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Status == SegmentOptionStatus.Archived)
        {
            throw new InvalidSegmentOptionStatusTransitionException(
                "An Archived SegmentOption cannot be edited.");
        }

        if (displayOrder < 0)
        {
            throw new ArgumentException("DisplayOrder cannot be negative.", nameof(displayOrder));
        }

        var normalizedDescription = NormalizeOptionalText(description, DescriptionMaxLength, nameof(description));
        var normalizedSemanticText = NormalizeOptionalText(semanticText, SemanticTextMaxLength, nameof(semanticText));

        Name = name;
        NormalizedName = SegmentTextNormalizer.Normalize(name.Value);
        Description = normalizedDescription;
        SemanticText = normalizedSemanticText;
        DisplayOrder = displayOrder;

        TransitionTo(status);
    }

    private void TransitionTo(SegmentOptionStatus newStatus)
    {
        if (newStatus == Status)
        {
            return;
        }

        if (!AllowedTransitions[Status].Contains(newStatus))
        {
            throw new InvalidSegmentOptionStatusTransitionException(
                $"Cannot transition SegmentOption status from {Status} to {newStatus}.");
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
