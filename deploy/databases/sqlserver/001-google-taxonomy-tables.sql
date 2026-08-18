-- Google Product Taxonomy import/synchronization schema
-- (docs task: "Implement the complete Google Product Taxonomy import/synchronization feature").
--
-- This script is idempotent: it can be re-run safely because it checks for
-- object existence before creating tables, constraints and indexes.
--
-- Local development execution:
--   sqlcmd -S <server> -d <database> -i deploy/sql/001-google-taxonomy-tables.sql
--
-- Production schema changes must go through a controlled migration process
-- (docs/data/data-architecture.md §48/Database Migrations); this script is not
-- executed automatically by the API host on startup.

IF SCHEMA_ID(N'Catalog') IS NULL EXEC(N'CREATE SCHEMA Catalog');
GO

IF SCHEMA_ID(N'Integration') IS NULL EXEC(N'CREATE SCHEMA Integration');
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GoogleTaxonomyCategories' AND s.name = 'Catalog')
BEGIN
    CREATE TABLE Catalog.GoogleTaxonomyCategories
    (
        GoogleCategoryId        INT             NOT NULL PRIMARY KEY,
        ParentGoogleCategoryId  INT             NULL,
        Name                    NVARCHAR(300)   NOT NULL,
        FullPath                NVARCHAR(2000)  NOT NULL,
        Level                   INT             NOT NULL,
        IsLeaf                  BIT             NOT NULL,
        IsActive                BIT             NOT NULL CONSTRAINT DF_GoogleTaxonomyCategories_IsActive DEFAULT (1),
        SourceLanguage          NVARCHAR(10)    NOT NULL,
        CreatedAt               DATETIME2       NOT NULL,
        UpdatedAt               DATETIME2       NULL,
        ImportedAt              DATETIME2       NOT NULL,

        CONSTRAINT FK_GoogleTaxonomyCategories_Parent
            FOREIGN KEY (ParentGoogleCategoryId)
            REFERENCES Catalog.GoogleTaxonomyCategories (GoogleCategoryId)
            -- No cascade delete: taxonomy rows are deactivated, never deleted.
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GoogleTaxonomyCategories_ParentGoogleCategoryId' AND object_id = OBJECT_ID('Catalog.GoogleTaxonomyCategories'))
BEGIN
    CREATE INDEX IX_GoogleTaxonomyCategories_ParentGoogleCategoryId ON Catalog.GoogleTaxonomyCategories (ParentGoogleCategoryId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GoogleTaxonomyCategories_Name' AND object_id = OBJECT_ID('Catalog.GoogleTaxonomyCategories'))
BEGIN
    CREATE INDEX IX_GoogleTaxonomyCategories_Name ON Catalog.GoogleTaxonomyCategories (Name);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GoogleTaxonomyCategories_Level' AND object_id = OBJECT_ID('Catalog.GoogleTaxonomyCategories'))
BEGIN
    CREATE INDEX IX_GoogleTaxonomyCategories_Level ON Catalog.GoogleTaxonomyCategories (Level);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GoogleTaxonomyCategories_IsActive' AND object_id = OBJECT_ID('Catalog.GoogleTaxonomyCategories'))
BEGIN
    CREATE INDEX IX_GoogleTaxonomyCategories_IsActive ON Catalog.GoogleTaxonomyCategories (IsActive);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GoogleTaxonomyCategories_FullPath' AND object_id = OBJECT_ID('Catalog.GoogleTaxonomyCategories'))
BEGIN
    CREATE INDEX IX_GoogleTaxonomyCategories_FullPath ON Catalog.GoogleTaxonomyCategories (FullPath);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'GoogleTaxonomyImports' AND s.name = 'Integration')
BEGIN
    CREATE TABLE Integration.GoogleTaxonomyImports
    (
        ImportId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        SourceLanguage     NVARCHAR(10)     NOT NULL,
        SourceUrl          NVARCHAR(1000)   NOT NULL,
        StartedAt          DATETIME2        NOT NULL,
        CompletedAt        DATETIME2        NULL,
        CategoryCount      INT              NOT NULL CONSTRAINT DF_GoogleTaxonomyImports_CategoryCount DEFAULT (0),
        InsertedCount      INT              NOT NULL CONSTRAINT DF_GoogleTaxonomyImports_InsertedCount DEFAULT (0),
        UpdatedCount       INT              NOT NULL CONSTRAINT DF_GoogleTaxonomyImports_UpdatedCount DEFAULT (0),
        DeactivatedCount   INT              NOT NULL CONSTRAINT DF_GoogleTaxonomyImports_DeactivatedCount DEFAULT (0),
        Status             NVARCHAR(30)     NOT NULL,
        ErrorMessage       NVARCHAR(2000)   NULL
    );
END
GO
