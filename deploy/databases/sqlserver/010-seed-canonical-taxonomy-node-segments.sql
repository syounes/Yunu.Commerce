/*
    Yunu.Commerce - Seed canonical taxonomy node segment definitions
    Target: SQL Server 2022+

    Preconditions:
      - 009-reset-canonical-taxonomy-starter.sql has already been executed.
      - Catalog.CanonicalTaxonomyNodes contains the canonical starter nodes.
      - Catalog.SegmentDefinitions contains the active segment definitions.
      - Catalog.CanonicalTaxonomyNodeSegmentDefinitions already exists.

    Curated initial mappings:

      Catálogo
        No segment is attached to the technical root. A root association would
        incorrectly make fashion-specific segmentation available to unrelated
        future branches such as electronics.

      Catálogo > Vestuário e acessórios
        - target_audience: required and inherited by descendants.
        - gender: optional and inherited by descendants.

      Catálogo > Vestuário e acessórios > Sapatos
        - sport_modality: optional and inherited by future descendants.

    foot_pronation is intentionally not attached to the broad Sapatos node.
    It should be associated when an appropriate descendant such as
    Tênis de corrida is created and approved.

    These mappings are curated Yunu catalog-engineering decisions, therefore:
      Source     = Yunu
      Confidence = NULL
      Status     = Approved

    The script is idempotent. It inserts missing mappings and updates the
    controlled fields of the three mappings without deleting unrelated rows.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes', N'U') IS NULL
        THROW 51050, 'Catalog.CanonicalTaxonomyNodes does not exist.', 1;

    IF OBJECT_ID(N'Catalog.SegmentDefinitions', N'U') IS NULL
        THROW 51051, 'Catalog.SegmentDefinitions does not exist.', 1;

    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodeSegmentDefinitions', N'U') IS NULL
        THROW 51052, 'Catalog.CanonicalTaxonomyNodeSegmentDefinitions does not exist.', 1;

    DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

    DECLARE @Mappings TABLE
    (
        NodeCode             NVARCHAR(120) NOT NULL,
        SegmentCode          NVARCHAR(100) NOT NULL,
        AppliesToDescendants BIT NOT NULL,
        IsRequired           BIT NOT NULL,

        PRIMARY KEY (NodeCode, SegmentCode)
    );

    INSERT INTO @Mappings
    (
        NodeCode,
        SegmentCode,
        AppliesToDescendants,
        IsRequired
    )
    VALUES
        (N'apparel_accessories', N'target_audience', 1, 1),
        (N'apparel_accessories', N'gender',          1, 0),
        (N'shoes',                N'sport_modality', 1, 0);

    /* Every mapped node must exist and be active. */
    IF EXISTS
    (
        SELECT 1
        FROM @Mappings AS Mapping
        LEFT JOIN Catalog.CanonicalTaxonomyNodes AS Node
            ON Node.Code = Mapping.NodeCode
           AND Node.Status = N'Active'
        WHERE Node.CanonicalTaxonomyNodeId IS NULL
    )
        THROW 51053, 'A mapped canonical taxonomy node does not exist or is not Active.', 1;

    /* Every mapped definition must exist and be active. */
    IF EXISTS
    (
        SELECT 1
        FROM @Mappings AS Mapping
        LEFT JOIN Catalog.SegmentDefinitions AS Definition
            ON Definition.Code = Mapping.SegmentCode
           AND Definition.Status = N'Active'
        WHERE Definition.SegmentDefinitionId IS NULL
    )
        THROW 51054, 'A mapped SegmentDefinition does not exist or is not Active.', 1;

    ;WITH ResolvedMappings AS
    (
        SELECT
            Node.CanonicalTaxonomyNodeId,
            Definition.SegmentDefinitionId,
            Mapping.AppliesToDescendants,
            Mapping.IsRequired
        FROM @Mappings AS Mapping
        INNER JOIN Catalog.CanonicalTaxonomyNodes AS Node
            ON Node.Code = Mapping.NodeCode
           AND Node.Status = N'Active'
        INNER JOIN Catalog.SegmentDefinitions AS Definition
            ON Definition.Code = Mapping.SegmentCode
           AND Definition.Status = N'Active'
    )
    MERGE Catalog.CanonicalTaxonomyNodeSegmentDefinitions WITH (HOLDLOCK) AS Target
    USING ResolvedMappings AS Source
       ON Target.CanonicalTaxonomyNodeId = Source.CanonicalTaxonomyNodeId
      AND Target.SegmentDefinitionId = Source.SegmentDefinitionId
    WHEN MATCHED THEN
        UPDATE SET
            AppliesToDescendants = Source.AppliesToDescendants,
            IsRequired           = Source.IsRequired,
            Source               = N'Yunu',
            Confidence           = NULL,
            Status               = N'Approved',
            UpdatedAt            = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            CanonicalTaxonomyNodeId,
            SegmentDefinitionId,
            AppliesToDescendants,
            IsRequired,
            Source,
            Confidence,
            Status,
            CreatedAt,
            UpdatedAt
        )
        VALUES
        (
            Source.CanonicalTaxonomyNodeId,
            Source.SegmentDefinitionId,
            Source.AppliesToDescendants,
            Source.IsRequired,
            N'Yunu',
            NULL,
            N'Approved',
            @Now,
            @Now
        );

    /* Validate the complete curated seed without touching unrelated mappings. */
    IF
    (
        SELECT COUNT(*)
        FROM @Mappings AS Mapping
        INNER JOIN Catalog.CanonicalTaxonomyNodes AS Node
            ON Node.Code = Mapping.NodeCode
        INNER JOIN Catalog.SegmentDefinitions AS Definition
            ON Definition.Code = Mapping.SegmentCode
        INNER JOIN Catalog.CanonicalTaxonomyNodeSegmentDefinitions AS Association
            ON Association.CanonicalTaxonomyNodeId = Node.CanonicalTaxonomyNodeId
           AND Association.SegmentDefinitionId = Definition.SegmentDefinitionId
           AND Association.AppliesToDescendants = Mapping.AppliesToDescendants
           AND Association.IsRequired = Mapping.IsRequired
           AND Association.Source = N'Yunu'
           AND Association.Confidence IS NULL
           AND Association.Status = N'Approved'
    ) <> (SELECT COUNT(*) FROM @Mappings)
        THROW 51055, 'Canonical taxonomy segment seed failed validation.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

/* Final verification: direct associations created/updated by this seed. */
SELECT
    Node.CanonicalTaxonomyNodeId,
    Node.Code AS CanonicalNodeCode,
    Node.Path AS CanonicalNodePath,
    Definition.SegmentDefinitionId,
    Definition.Code AS SegmentCode,
    Definition.Name AS SegmentName,
    Definition.SelectionMode,
    Definition.AssignmentScope,
    Association.AppliesToDescendants,
    Association.IsRequired,
    Association.Source,
    Association.Confidence,
    Association.Status,
    Association.CreatedAt,
    Association.UpdatedAt
FROM Catalog.CanonicalTaxonomyNodeSegmentDefinitions AS Association
INNER JOIN Catalog.CanonicalTaxonomyNodes AS Node
    ON Node.CanonicalTaxonomyNodeId = Association.CanonicalTaxonomyNodeId
INNER JOIN Catalog.SegmentDefinitions AS Definition
    ON Definition.SegmentDefinitionId = Association.SegmentDefinitionId
WHERE Association.Source = N'Yunu'
  AND Association.Status = N'Approved'
ORDER BY Node.Depth, Node.Path, Definition.Code;
