/*
    Yunu.Commerce - Remove Canonical Taxonomy provider coupling
    Target: SQL Server 2022+

    Decision (docs task: "Yunu.Commerce - Canonical Taxonomy Provider
    Decoupling"; docs/adr/0014-provider-neutral-source-taxonomy.md):
      CanonicalTaxonomyNode must represent only approved canonical catalog
      truth. It must not know Google, Mercado Livre, Amazon or any other
      upstream provider, must not store an external/provider taxonomy node
      identifier, and must not record how the node was originally produced.
      A canonical node may eventually be supported by 0..N external source
      nodes, so no provider ID is stored directly on the canonical row.
      Future external evidence belongs to the future SourceTaxonomy /
      SourceTaxonomyNode model; future AI-generation / human-governance
      provenance belongs to future proposal/review workflow metadata, not to
      Catalog.CanonicalTaxonomyNodes.

    This migration removes the obsolete provider identity/provenance columns:
      - Catalog.CanonicalTaxonomyNodes.GoogleCategoryId
      - Catalog.CanonicalTaxonomyNodes.Source

    Explicitly NOT changed by this migration:
      - No canonical node is deleted.
      - No CanonicalTaxonomyNodeId value is altered/reseeded.
      - ParentId, Code, Name, Path, Status and Revision are left untouched.
      - Catalog.CanonicalTaxonomyNodeSegmentDefinitions.Source (association
        provenance: Yunu/AI) is a completely different column on a different
        table and is NOT touched here.
      - Catalog.GoogleTaxonomyCategories, GoogleTaxonomy ingestion and the
        Google-specific resolver/embeddings stack are NOT touched here
        (docs/adr/0014 retains them pending SourceTaxonomy parity).

    Dependencies removed before dropping the columns:
      - UX_CanonicalTaxonomyNodes_GoogleCategoryId (unique filtered index)
      - CK_CanonicalTaxonomyNodes_GoogleCategoryId (check constraint)
      - CK_CanonicalTaxonomyNodes_GoogleSource (check constraint)
      - CK_CanonicalTaxonomyNodes_Source (check constraint)
      - DF_CanonicalTaxonomyNodes_Source (default constraint)
      - IX_CanonicalTaxonomyNodes_ParentId_Status and
        IX_CanonicalTaxonomyNodes_NormalizedName_Status, both of which
        currently INCLUDE (GoogleCategoryId, Source); these are dropped and
        recreated with provider-neutral INCLUDE lists that preserve their
        original (parent/status and normalized-name/status) query purpose.

    The script is idempotent: every step is guarded by an existence check
    against sys.indexes / sys.check_constraints / sys.default_constraints /
    COL_LENGTH, so it can be safely re-executed.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes', N'U') IS NULL
        THROW 51080, 'Catalog.CanonicalTaxonomyNodes does not exist.', 1;

    /* ================================================================
       Drop indexes that currently INCLUDE the obsolete columns, so the
       columns themselves can later be dropped.
       ================================================================ */
    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'IX_CanonicalTaxonomyNodes_ParentId_Status'
    )
        DROP INDEX IX_CanonicalTaxonomyNodes_ParentId_Status
            ON Catalog.CanonicalTaxonomyNodes;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'IX_CanonicalTaxonomyNodes_NormalizedName_Status'
    )
        DROP INDEX IX_CanonicalTaxonomyNodes_NormalizedName_Status
            ON Catalog.CanonicalTaxonomyNodes;

    /* Obsolete filtered unique index on GoogleCategoryId. */
    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'UX_CanonicalTaxonomyNodes_GoogleCategoryId'
    )
        DROP INDEX UX_CanonicalTaxonomyNodes_GoogleCategoryId
            ON Catalog.CanonicalTaxonomyNodes;

    /* ================================================================
       Drop CHECK constraints referencing the obsolete columns.
       ================================================================ */
    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'CK_CanonicalTaxonomyNodes_GoogleCategoryId'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT CK_CanonicalTaxonomyNodes_GoogleCategoryId;

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'CK_CanonicalTaxonomyNodes_GoogleSource'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT CK_CanonicalTaxonomyNodes_GoogleSource;

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'CK_CanonicalTaxonomyNodes_Source'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT CK_CanonicalTaxonomyNodes_Source;

    /* ================================================================
       Drop the DEFAULT constraint on Source, if still present.
       ================================================================ */
    IF EXISTS
    (
        SELECT 1 FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'DF_CanonicalTaxonomyNodes_Source'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT DF_CanonicalTaxonomyNodes_Source;

    /* A default constraint on Source may also have been created unnamed by
       an earlier environment; drop any remaining default bound to the
       Source column defensively before dropping the column itself. */
    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'Source') IS NOT NULL
    BEGIN
        DECLARE @SourceDefaultConstraint SYSNAME;

        SELECT @SourceDefaultConstraint = dc.name
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND c.name = N'Source';

        IF @SourceDefaultConstraint IS NOT NULL
        BEGIN
            DECLARE @DropSourceDefaultSql NVARCHAR(MAX) =
                N'ALTER TABLE Catalog.CanonicalTaxonomyNodes DROP CONSTRAINT ' +
                QUOTENAME(@SourceDefaultConstraint) + N';';

            EXEC sys.sp_executesql @DropSourceDefaultSql;
        END;
    END;

    /* A default constraint may similarly remain bound to GoogleCategoryId in
       some environments even though none was named explicitly by earlier
       migrations; drop it defensively before dropping the column. */
    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'GoogleCategoryId') IS NOT NULL
    BEGIN
        DECLARE @GoogleCategoryIdDefaultConstraint SYSNAME;

        SELECT @GoogleCategoryIdDefaultConstraint = dc.name
        FROM sys.default_constraints AS dc
        INNER JOIN sys.columns AS c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND c.name = N'GoogleCategoryId';

        IF @GoogleCategoryIdDefaultConstraint IS NOT NULL
        BEGIN
            DECLARE @DropGoogleCategoryIdDefaultSql NVARCHAR(MAX) =
                N'ALTER TABLE Catalog.CanonicalTaxonomyNodes DROP CONSTRAINT ' +
                QUOTENAME(@GoogleCategoryIdDefaultConstraint) + N';';

            EXEC sys.sp_executesql @DropGoogleCategoryIdDefaultSql;
        END;
    END;

    /* ================================================================
       Drop the obsolete provider identity/provenance columns.
       ================================================================ */
    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'GoogleCategoryId') IS NOT NULL
        ALTER TABLE Catalog.CanonicalTaxonomyNodes DROP COLUMN GoogleCategoryId;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'Source') IS NOT NULL
        ALTER TABLE Catalog.CanonicalTaxonomyNodes DROP COLUMN Source;

    /* ================================================================
       Recreate the parent/status and normalized-name/status indexes,
       provider-neutral: same key columns, INCLUDE lists no longer
       reference GoogleCategoryId/Source.
       ================================================================ */
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'IX_CanonicalTaxonomyNodes_ParentId_Status'
    )
        CREATE INDEX IX_CanonicalTaxonomyNodes_ParentId_Status
            ON Catalog.CanonicalTaxonomyNodes (ParentId, Status)
            INCLUDE (Code, Name, Depth, Path);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'IX_CanonicalTaxonomyNodes_NormalizedName_Status'
    )
        CREATE INDEX IX_CanonicalTaxonomyNodes_NormalizedName_Status
            ON Catalog.CanonicalTaxonomyNodes (NormalizedName, Status)
            INCLUDE (Code, Path);

    /* ================================================================
       Final structural verification: existing rows, identities and the
       parent/child hierarchy must all survive this migration untouched.
       ================================================================ */
    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'GoogleCategoryId') IS NOT NULL
        THROW 51081, 'Catalog.CanonicalTaxonomyNodes.GoogleCategoryId still exists after migration.', 1;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'Source') IS NOT NULL
        THROW 51082, 'Catalog.CanonicalTaxonomyNodes.Source still exists after migration.', 1;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'CanonicalTaxonomyNodeId') IS NULL
        THROW 51083, 'Catalog.CanonicalTaxonomyNodes.CanonicalTaxonomyNodeId is missing after migration.', 1;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'ParentId') IS NULL
        THROW 51084, 'Catalog.CanonicalTaxonomyNodes.ParentId is missing after migration.', 1;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'Revision') IS NOT NULL
       AND EXISTS (SELECT 1 FROM Catalog.CanonicalTaxonomyNodes WHERE Revision <= 0)
        THROW 51085, 'Catalog.CanonicalTaxonomyNodes contains an invalid Revision after migration.', 1;

    /* Catalog.CanonicalTaxonomyNodeSegmentDefinitions.Source is a distinct
       association-provenance column (Yunu/AI) on a different table and must
       remain completely untouched by this migration. */
    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodeSegmentDefinitions', N'U') IS NOT NULL
       AND COL_LENGTH(N'Catalog.CanonicalTaxonomyNodeSegmentDefinitions', N'Source') IS NULL
        THROW 51086, 'Catalog.CanonicalTaxonomyNodeSegmentDefinitions.Source was unexpectedly removed.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
