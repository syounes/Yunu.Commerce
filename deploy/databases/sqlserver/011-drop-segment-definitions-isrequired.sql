/*
    Yunu.Commerce - Consolidate IsRequired semantics for Segments
    Target: SQL Server 2022+

    Decision (docs task: "Consolidar a sem�ntica de IsRequired em Segments"):
      Obligatoriness of a Segment Definition is contextual to where it is
      associated in the Canonical Taxonomy, never a global property of the
      Definition itself. Catalog.CanonicalTaxonomyNodeSegmentDefinitions.IsRequired
      is therefore the single source of truth for obligatoriness; the
      duplicate, catalog-wide Catalog.SegmentDefinitions.IsRequired column
      never had independent functional semantics (it was not read by
      SegmentAssignmentResolver, Product/Sku assignment, or by the semantic
      embedding pipeline, which already excluded it from generated text) and
      is removed here.

    Preserved unchanged:
      - Catalog.CanonicalTaxonomyNodeSegmentDefinitions.IsRequired.
      - The CanonicalNode <-> SegmentDefinition many-to-many relationship.
      - AppliesToDescendants, Source, Status, Confidence.
      - Effective Segment Definitions resolution and its precedence rule.

    The script is idempotent: it only drops the default constraint, the
    IsRequired column and updates the two supporting indexes when they still
    reference it.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'Catalog.SegmentDefinitions', N'U') IS NULL
        THROW 51060, 'Catalog.SegmentDefinitions does not exist.', 1;

    /* Drop the default constraint first, if it still exists. */
    IF EXISTS
    (
        SELECT 1
        FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SegmentDefinitions')
          AND name = N'DF_SegmentDefinitions_IsRequired'
    )
    BEGIN
        ALTER TABLE Catalog.SegmentDefinitions
            DROP CONSTRAINT DF_SegmentDefinitions_IsRequired;
    END;

    /* Rebuild indexes that INCLUDE(IsRequired) so the column can be dropped. */
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SegmentDefinitions')
          AND name = N'IX_SegmentDefinitions_Status'
    )
    BEGIN
        DROP INDEX IX_SegmentDefinitions_Status ON Catalog.SegmentDefinitions;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SegmentDefinitions')
          AND name = N'IX_SegmentDefinitions_AssignmentScope_Status'
    )
    BEGIN
        DROP INDEX IX_SegmentDefinitions_AssignmentScope_Status ON Catalog.SegmentDefinitions;
    END;

    IF COL_LENGTH(N'Catalog.SegmentDefinitions', N'IsRequired') IS NOT NULL
    BEGIN
        ALTER TABLE Catalog.SegmentDefinitions
            DROP COLUMN IsRequired;
    END;

    CREATE INDEX IX_SegmentDefinitions_Status
        ON Catalog.SegmentDefinitions (Status)
        INCLUDE (Code, Name, SelectionMode);

    CREATE INDEX IX_SegmentDefinitions_AssignmentScope_Status
        ON Catalog.SegmentDefinitions (AssignmentScope, Status)
        INCLUDE (Code, Name, SelectionMode);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

/* Verification: IsRequired must no longer exist on Catalog.SegmentDefinitions. */
SELECT
    ColumnExists = COL_LENGTH(N'Catalog.SegmentDefinitions', N'IsRequired');
