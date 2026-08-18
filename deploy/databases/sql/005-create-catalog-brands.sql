-- Idempotent creation of Catalog.Brands table and minimal seed
-- Creates schema if missing and seeds a single canonical brand: YUNU
--
-- Column naming follows the existing Catalog SQL convention: physical
-- timestamp columns are named CreatedAt / UpdatedAt (not CreatedAtUtc /
-- UpdatedAtUtc), consistent with other Catalog reference tables. Values
-- stored in these columns still represent UTC instants (DATETIMEOFFSET);
-- only the physical column naming changes, not the semantic convention.

IF SCHEMA_ID(N'Catalog') IS NULL EXEC(N'CREATE SCHEMA Catalog');
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = 'Brands' AND s.name = 'Catalog')
BEGIN
    CREATE TABLE Catalog.Brands
    (
        BrandId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Code VARCHAR(12) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        NormalizedName NVARCHAR(200) NOT NULL,
        Status VARCHAR(20) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NULL
    );

    CREATE UNIQUE INDEX IX_Catalog_Brands_Code ON Catalog.Brands(Code);
    CREATE INDEX IX_Catalog_Brands_NormalizedName ON Catalog.Brands(NormalizedName);
END
GO

-- Corrective migration: earlier deployments of this script may have created
-- the columns as CreatedAtUtc/UpdatedAtUtc. Rename them to the canonical
-- Catalog naming convention (CreatedAt/UpdatedAt) without losing data.
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'Catalog' AND t.name = 'Brands' AND c.name = 'CreatedAtUtc')
BEGIN
    EXEC sp_rename 'Catalog.Brands.CreatedAtUtc', 'CreatedAt', 'COLUMN';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'Catalog' AND t.name = 'Brands' AND c.name = 'UpdatedAtUtc')
BEGIN
    EXEC sp_rename 'Catalog.Brands.UpdatedAtUtc', 'UpdatedAt', 'COLUMN';
END
GO

-- Corrective migration: earlier deployments of this script may have created
-- CreatedAt/UpdatedAt (or their prior CreatedAtUtc/UpdatedAtUtc names) as
-- DATETIME2 instead of DATETIMEOFFSET. Align the column types so
-- SqlDataReader.GetDateTimeOffset does not fail at runtime.
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'Catalog' AND t.name = 'Brands' AND c.name = 'CreatedAt' AND ty.name <> 'datetimeoffset')
BEGIN
    ALTER TABLE Catalog.Brands ALTER COLUMN CreatedAt DATETIMEOFFSET NOT NULL;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'Catalog' AND t.name = 'Brands' AND c.name = 'UpdatedAt' AND ty.name <> 'datetimeoffset')
BEGIN
    ALTER TABLE Catalog.Brands ALTER COLUMN UpdatedAt DATETIMEOFFSET NULL;
END
GO

-- Seed canonical YUNU brand with a stable, permanently persisted BrandId.
-- This value must never change across deployments once seeded.
DECLARE @yunuId UNIQUEIDENTIFIER = '4d6f7f2e-9b8a-4c2d-8f1a-6c1e2a3b4d5e';

IF NOT EXISTS (SELECT 1 FROM Catalog.Brands WHERE BrandId = @yunuId)
BEGIN
    INSERT INTO Catalog.Brands (BrandId, Code, Name, NormalizedName, Status, CreatedAt)
    VALUES (@yunuId, 'YUNU', 'YUNU', 'YUNU', 'Active', SYSUTCDATETIME());
END
GO

