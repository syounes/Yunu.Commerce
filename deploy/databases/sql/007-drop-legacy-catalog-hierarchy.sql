/*
    Yunu.Commerce - Remove legacy catalog hierarchy
    Target: SQL Server 2022+

    The following legacy tables were replaced by Catalog.CanonicalTaxonomyNodes:
      - Catalog.Departments
      - Catalog.Categories
      - Catalog.SubCategories
      - Catalog.Families

    SQL Server removes indexes, constraints and triggers owned by a table when
    the table is dropped. This script explicitly removes inbound foreign keys
    first, including foreign keys declared by tables outside this legacy set.

    Intentionally preserved:
      - Catalog.AttributeDefinitions
      - Catalog.AttributeGroups
      - Catalog.AttributeOptions
      - Catalog.Brands
      - Catalog.CanonicalTaxonomyNodes
      - Catalog.GoogleCategoryAttributeRules
      - Catalog.GoogleTaxonomyCategories
      - Catalog.SegmentDefinitions
      - Catalog.SegmentOptions
      - Catalog.SkuAttributeValues
      - Integration.AttributeEmbeddingOutbox
      - Integration.GoogleTaxonomyImports
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LegacyTables TABLE
    (
        SchemaName SYSNAME NOT NULL,
        TableName  SYSNAME NOT NULL,
        DropOrder  INT NOT NULL,

        PRIMARY KEY (SchemaName, TableName)
    );

    INSERT INTO @LegacyTables (SchemaName, TableName, DropOrder)
    VALUES
        (N'Catalog', N'SubCategories', 10),
        (N'Catalog', N'Families',      20),
        (N'Catalog', N'Categories',    30),
        (N'Catalog', N'Departments',   40);

    /*
        Drop every foreign key that references a legacy table.
        This includes relationships between the legacy tables themselves and
        relationships declared by any remaining table.
    */
    DECLARE @DropForeignKeysSql NVARCHAR(MAX);

    SELECT
        @DropForeignKeysSql = STRING_AGG
        (
            CAST
            (
                N'ALTER TABLE '
                + QUOTENAME(OBJECT_SCHEMA_NAME(ForeignKey.parent_object_id))
                + N'.'
                + QUOTENAME(OBJECT_NAME(ForeignKey.parent_object_id))
                + N' DROP CONSTRAINT '
                + QUOTENAME(ForeignKey.name)
                + N';'
                AS NVARCHAR(MAX)
            ),
            CHAR(13) + CHAR(10)
        ) WITHIN GROUP (ORDER BY ForeignKey.object_id)
    FROM sys.foreign_keys AS ForeignKey
    INNER JOIN @LegacyTables AS LegacyTable
        ON OBJECT_SCHEMA_NAME(ForeignKey.referenced_object_id) = LegacyTable.SchemaName
       AND OBJECT_NAME(ForeignKey.referenced_object_id) = LegacyTable.TableName;

    IF NULLIF(@DropForeignKeysSql, N'') IS NOT NULL
        EXEC sys.sp_executesql @DropForeignKeysSql;

    /*
        Child-to-parent order is explicit for readability. Because inbound
        foreign keys were already removed, rerunning this script is safe.
    */
    DROP TABLE IF EXISTS Catalog.SubCategories;
    DROP TABLE IF EXISTS Catalog.Families;
    DROP TABLE IF EXISTS Catalog.Categories;
    DROP TABLE IF EXISTS Catalog.Departments;

    /* Fail the deployment if any target unexpectedly remains. */
    IF EXISTS
    (
        SELECT 1
        FROM @LegacyTables AS LegacyTable
        WHERE OBJECT_ID
        (
            QUOTENAME(LegacyTable.SchemaName)
            + N'.'
            + QUOTENAME(LegacyTable.TableName),
            N'U'
        ) IS NOT NULL
    )
        THROW 51010, 'One or more legacy catalog hierarchy tables could not be removed.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

/* Verification: expected result is zero rows. */
SELECT
    SchemaName = SchemaValue.name,
    TableName = TableValue.name
FROM sys.tables AS TableValue
INNER JOIN sys.schemas AS SchemaValue
    ON SchemaValue.schema_id = TableValue.schema_id
WHERE SchemaValue.name = N'Catalog'
  AND TableValue.name IN
  (
      N'Departments',
      N'Categories',
      N'SubCategories',
      N'Families'
  )
ORDER BY TableValue.name;
