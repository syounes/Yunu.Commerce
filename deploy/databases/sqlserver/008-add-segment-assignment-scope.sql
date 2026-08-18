/*
    Yunu.Commerce - Segment assignment scope
    Target: SQL Server 2022+

    SQL Server remains the source of truth for where each SegmentDefinition
    may be assigned. Domain code will enforce this rule in a later step.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'Catalog.SegmentDefinitions', N'U') IS NULL
        THROW 51020, 'Catalog.SegmentDefinitions does not exist.', 1;

    /* Nullable first so the migration also works against existing data. */
    IF COL_LENGTH(N'Catalog.SegmentDefinitions', N'AssignmentScope') IS NULL
    BEGIN
        ALTER TABLE Catalog.SegmentDefinitions
            ADD AssignmentScope VARCHAR(32) NULL;
    END;

    /*
        Dynamic SQL avoids SQL Server batch binding failures when the column
        is created and referenced in the same migration batch.
    */
    EXEC sys.sp_executesql N'
        UPDATE Catalog.SegmentDefinitions
        SET
            AssignmentScope = CASE
                WHEN Code IN (''target_audience'', ''gender'')
                    THEN ''ProductWithSkuOverride''
                WHEN Code IN
                (
                    ''sport_modality'',
                    ''foot_pronation'',
                    ''computer_profile''
                )
                    THEN ''Product''
                ELSE COALESCE(AssignmentScope, ''Product'')
            END,
            UpdatedAt = SYSUTCDATETIME()
        WHERE AssignmentScope IS NULL
           OR AssignmentScope <> CASE
                WHEN Code IN (''target_audience'', ''gender'')
                    THEN ''ProductWithSkuOverride''
                WHEN Code IN
                (
                    ''sport_modality'',
                    ''foot_pronation'',
                    ''computer_profile''
                )
                    THEN ''Product''
                ELSE AssignmentScope
              END;
    ';

    EXEC sys.sp_executesql N'
        IF EXISTS
        (
            SELECT 1
            FROM Catalog.SegmentDefinitions
            WHERE AssignmentScope IS NULL
               OR AssignmentScope NOT IN
                  (
                      ''Product'',
                      ''Sku'',
                      ''ProductWithSkuOverride''
                  )
        )
            THROW 51021, ''Invalid SegmentDefinition AssignmentScope detected.'', 1;
    ';

    EXEC sys.sp_executesql N'
        ALTER TABLE Catalog.SegmentDefinitions
            ALTER COLUMN AssignmentScope VARCHAR(32) NOT NULL;
    ';

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SegmentDefinitions')
          AND parent_column_id = COLUMNPROPERTY
          (
              OBJECT_ID(N'Catalog.SegmentDefinitions'),
              N'AssignmentScope',
              N'ColumnId'
          )
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE Catalog.SegmentDefinitions
                ADD CONSTRAINT DF_SegmentDefinitions_AssignmentScope
                DEFAULT (''Product'') FOR AssignmentScope;
        ';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SegmentDefinitions')
          AND name = N'CK_SegmentDefinitions_AssignmentScope'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE Catalog.SegmentDefinitions WITH CHECK
                ADD CONSTRAINT CK_SegmentDefinitions_AssignmentScope
                CHECK
                (
                    AssignmentScope IN
                    (
                        ''Product'',
                        ''Sku'',
                        ''ProductWithSkuOverride''
                    )
                );

            ALTER TABLE Catalog.SegmentDefinitions
                CHECK CONSTRAINT CK_SegmentDefinitions_AssignmentScope;
        ';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SegmentDefinitions')
          AND name = N'IX_SegmentDefinitions_AssignmentScope_Status'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_SegmentDefinitions_AssignmentScope_Status
                ON Catalog.SegmentDefinitions (AssignmentScope, Status)
                INCLUDE (Code, Name, SelectionMode, IsRequired);
        ';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

EXEC sys.sp_executesql N'
    SELECT
        SegmentDefinitionId,
        Code,
        Name,
        SelectionMode,
        AssignmentScope,
        IsRequired,
        Status
    FROM Catalog.SegmentDefinitions
    ORDER BY Code;
';
