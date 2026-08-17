-- Idempotent creation of Catalog.Brands table and minimal seed
-- Creates schema if missing and seeds a single canonical brand: YUNU

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
        CreatedAtUtc DATETIME2 NOT NULL,
        UpdatedAtUtc DATETIME2 NULL
    );

    CREATE UNIQUE INDEX IX_Catalog_Brands_Code ON Catalog.Brands(Code);
    CREATE INDEX IX_Catalog_Brands_NormalizedName ON Catalog.Brands(NormalizedName);
END
GO

-- Seed canonical YUNU brand with stable BrandId
DECLARE @yunuId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

IF NOT EXISTS (SELECT 1 FROM Catalog.Brands WHERE BrandId = @yunuId)
BEGIN
    INSERT INTO Catalog.Brands (BrandId, Code, Name, NormalizedName, Status, CreatedAtUtc)
    VALUES (@yunuId, 'YUNU', 'YUNU', 'YUNU', 'Active', SYSUTCDATETIME());
END
GO
