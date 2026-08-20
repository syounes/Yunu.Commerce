/*
    Yunu.Commerce - Canonical Taxonomy starter model and data reset
    Target: SQL Server 2022+

    Resulting tree:
      Catálogo
      └── Vestuário e acessórios (Google 166)
          └── Sapatos (Google 187)

    Decisions implemented:
      - Canonical nodes initially mirror Google Taxonomy; AI completes the tree later.
      - Catálogo is the Yunu technical root at Depth 0.
      - Node Source accepts only Yunu, Google or AI.
      - Google nodes must have GoogleCategoryId; AI nodes should start as Draft.
      - Path stores pt-BR names separated by " > "; it is not a URL.
      - Stable Code values remain in English.
      - A node can expose zero or many SegmentDefinitions.
      - SegmentDefinitionId is removed from CanonicalTaxonomyNodes.
      - Catalog.CanonicalTaxonomyNodeSegmentDefinitions owns the many-to-many
        relationship and supports AI suggestion followed by human approval.

    WARNING:
      This migration deletes all canonical nodes and all node-to-segment mappings
      before inserting the validated three-node starter tree. MongoDB Product
      documents may reference old CanonicalTaxonomyNodeId values. Execute only
      during bootstrap or after clearing/migrating the related development data.

      The C# model/repository must subsequently change from one
      SegmentDefinitionId to a collection backed by the junction table. Path
      keeps its current name, but its builder must use names and " > ".
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes', N'U') IS NULL
        THROW 51030, 'Catalog.CanonicalTaxonomyNodes does not exist.', 1;

    IF OBJECT_ID(N'Catalog.SegmentDefinitions', N'U') IS NULL
        THROW 51031, 'Catalog.SegmentDefinitions does not exist.', 1;

    IF OBJECT_ID(N'Catalog.GoogleTaxonomyCategories', N'U') IS NULL
        THROW 51032, 'Catalog.GoogleTaxonomyCategories does not exist.', 1;

    DECLARE @ApparelAndAccessoriesGoogleCategoryId BIGINT = 166;
    DECLARE @ShoesGoogleCategoryId BIGINT = 187;

    /* Validate the Google pt-BR source before destructive work. */
    IF NOT EXISTS
    (
        SELECT 1
        FROM Catalog.GoogleTaxonomyCategories
        WHERE GoogleCategoryId = @ApparelAndAccessoriesGoogleCategoryId
          AND ParentGoogleCategoryId IS NULL
          AND Name = N'Vestuário e acessórios'
          AND FullPath = N'Vestuário e acessórios'
          AND SourceLanguage = N'pt-BR'
          AND IsActive = 1
    )
        THROW 51033, 'Google category 166 does not match the expected active pt-BR source.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Catalog.GoogleTaxonomyCategories
        WHERE GoogleCategoryId = @ShoesGoogleCategoryId
          AND ParentGoogleCategoryId = @ApparelAndAccessoriesGoogleCategoryId
          AND Name = N'Sapatos'
          AND FullPath = N'Vestuário e acessórios > Sapatos'
          AND SourceLanguage = N'pt-BR'
          AND IsActive = 1
    )
        THROW 51034, 'Google category 187 does not match Sapatos under category 166 in pt-BR.', 1;

    /* Clear an existing junction table first, making reruns safe. */
    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodeSegmentDefinitions', N'U') IS NOT NULL
        EXEC(N'DELETE FROM Catalog.CanonicalTaxonomyNodeSegmentDefinitions;');

    /* Delete leaves first while preserving the trusted self-reference FK. */
    WHILE EXISTS (SELECT 1 FROM Catalog.CanonicalTaxonomyNodes)
    BEGIN
        DELETE Node
        FROM Catalog.CanonicalTaxonomyNodes AS Node
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM Catalog.CanonicalTaxonomyNodes AS Child
            WHERE Child.ParentId = Node.CanonicalTaxonomyNodeId
        );

        IF @@ROWCOUNT = 0
            THROW 51035, 'Canonical taxonomy contains a cycle and could not be reset safely.', 1;
    END;

    /* ================================================================
       Remove the obsolete one-definition-per-node relationship.
       ================================================================ */
    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'IX_CanonicalTaxonomyNodes_SegmentDefinitionId'
    )
        DROP INDEX IX_CanonicalTaxonomyNodes_SegmentDefinitionId
            ON Catalog.CanonicalTaxonomyNodes;

    /* These indexes include the obsolete SegmentDefinitionId column. */
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

    IF EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'FK_CanonicalTaxonomyNodes_SegmentDefinition'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT FK_CanonicalTaxonomyNodes_SegmentDefinition;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'SegmentDefinitionId') IS NOT NULL
        ALTER TABLE Catalog.CanonicalTaxonomyNodes DROP COLUMN SegmentDefinitionId;

    IF COL_LENGTH(N'Catalog.CanonicalTaxonomyNodes', N'Path') IS NULL
        THROW 51036, 'Catalog.CanonicalTaxonomyNodes.Path does not exist.', 1;

    /* ================================================================
       Restrict node provenance to Yunu, Google or AI.
       ================================================================ */
    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'CK_CanonicalTaxonomyNodes_Source'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT CK_CanonicalTaxonomyNodes_Source;

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes')
          AND name = N'CK_CanonicalTaxonomyNodes_GoogleSource'
    )
        ALTER TABLE Catalog.CanonicalTaxonomyNodes
            DROP CONSTRAINT CK_CanonicalTaxonomyNodes_GoogleSource;

    ALTER TABLE Catalog.CanonicalTaxonomyNodes WITH CHECK
        ADD CONSTRAINT CK_CanonicalTaxonomyNodes_Source
            CHECK (Source IN (N'Yunu', N'Google', N'AI'));

    ALTER TABLE Catalog.CanonicalTaxonomyNodes WITH CHECK
        ADD CONSTRAINT CK_CanonicalTaxonomyNodes_GoogleSource
            CHECK (Source <> N'Google' OR GoogleCategoryId IS NOT NULL);

    CREATE INDEX IX_CanonicalTaxonomyNodes_ParentId_Status
        ON Catalog.CanonicalTaxonomyNodes (ParentId, Status)
        INCLUDE (Code, Name, Depth, Path, GoogleCategoryId, Source);

    CREATE INDEX IX_CanonicalTaxonomyNodes_NormalizedName_Status
        ON Catalog.CanonicalTaxonomyNodes (NormalizedName, Status)
        INCLUDE (Code, Path, GoogleCategoryId, Source);

    /* ================================================================
       Many-to-many Canonical Node <-> Segment Definition association.

       Association Source:
         Yunu = curated directly by catalog engineering.
         AI   = proposed by an AI resolver.

       Association Status:
         Suggested = AI proposal; not effective yet.
         Approved  = usable by Product/Sku resolution.
         Rejected  = reviewed and rejected.
         Inactive  = previously approved but no longer applicable.
       ================================================================ */
    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodeSegmentDefinitions', N'U') IS NULL
    BEGIN
        CREATE TABLE Catalog.CanonicalTaxonomyNodeSegmentDefinitions
        (
            CanonicalTaxonomyNodeId BIGINT NOT NULL,
            SegmentDefinitionId     BIGINT NOT NULL,
            AppliesToDescendants    BIT NOT NULL
                CONSTRAINT DF_CanonicalNodeSegments_AppliesToDescendants DEFAULT (0),
            IsRequired              BIT NOT NULL
                CONSTRAINT DF_CanonicalNodeSegments_IsRequired DEFAULT (0),
            Source                  NVARCHAR(16) NOT NULL,
            Confidence              DECIMAL(5, 4) NULL,
            Status                  NVARCHAR(16) NOT NULL
                CONSTRAINT DF_CanonicalNodeSegments_Status DEFAULT (N'Suggested'),
            CreatedAt               DATETIME2(7) NOT NULL
                CONSTRAINT DF_CanonicalNodeSegments_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt               DATETIME2(7) NOT NULL
                CONSTRAINT DF_CanonicalNodeSegments_UpdatedAt DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_CanonicalTaxonomyNodeSegmentDefinitions
                PRIMARY KEY CLUSTERED
                (CanonicalTaxonomyNodeId, SegmentDefinitionId),

            -- Intentionally no ON DELETE CASCADE (docs task: "Yunu.Commerce
            -- V9 - Canonical Taxonomy Lifecycle + Usage Guards"): Canonical
            -- Taxonomy nodes are never hard-deleted, so no cascade delete
            -- path should exist at the structural (FK) level either.
            CONSTRAINT FK_CanonicalNodeSegments_Node
                FOREIGN KEY (CanonicalTaxonomyNodeId)
                REFERENCES Catalog.CanonicalTaxonomyNodes (CanonicalTaxonomyNodeId),

            CONSTRAINT FK_CanonicalNodeSegments_Definition
                FOREIGN KEY (SegmentDefinitionId)
                REFERENCES Catalog.SegmentDefinitions (SegmentDefinitionId),

            CONSTRAINT CK_CanonicalNodeSegments_Source
                CHECK (Source IN (N'Yunu', N'AI')),

            CONSTRAINT CK_CanonicalNodeSegments_Confidence
                CHECK (Confidence IS NULL OR Confidence BETWEEN 0.0000 AND 1.0000),

            CONSTRAINT CK_CanonicalNodeSegments_Status
                CHECK (Status IN (N'Suggested', N'Approved', N'Rejected', N'Inactive')),

            CONSTRAINT CK_CanonicalNodeSegments_Dates
                CHECK (UpdatedAt >= CreatedAt),

            CONSTRAINT CK_CanonicalNodeSegments_AIConfidence
                CHECK (Source <> N'AI' OR Confidence IS NOT NULL)
        );

        CREATE INDEX IX_CanonicalNodeSegments_Definition_Status
            ON Catalog.CanonicalTaxonomyNodeSegmentDefinitions
                (SegmentDefinitionId, Status)
            INCLUDE
                (CanonicalTaxonomyNodeId, AppliesToDescendants, IsRequired, Source, Confidence);

        CREATE INDEX IX_CanonicalNodeSegments_Node_Status
            ON Catalog.CanonicalTaxonomyNodeSegmentDefinitions
                (CanonicalTaxonomyNodeId, Status)
            INCLUDE
                (SegmentDefinitionId, AppliesToDescendants, IsRequired, Source, Confidence);
    END;

    DBCC CHECKIDENT (N'Catalog.CanonicalTaxonomyNodes', RESEED, 0)
        WITH NO_INFOMSGS;

    DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
    DECLARE @CatalogNodeId BIGINT;
    DECLARE @ApparelAndAccessoriesNodeId BIGINT;

    INSERT INTO Catalog.CanonicalTaxonomyNodes
    (
        ParentId, Code, Name, NormalizedName, Description, Depth, Path,
        GoogleCategoryId, Source, Status, CreatedAt, UpdatedAt
    )
    VALUES
    (
        NULL, N'catalog', N'Catálogo', N'catalogo',
        N'Raiz técnica da taxonomia canônica do Yunu.Commerce.',
        0, N'Catálogo', NULL, N'Yunu', N'Active', @Now, @Now
    );

    SET @CatalogNodeId = CONVERT(BIGINT, SCOPE_IDENTITY());

    INSERT INTO Catalog.CanonicalTaxonomyNodes
    (
        ParentId, Code, Name, NormalizedName, Description, Depth, Path,
        GoogleCategoryId, Source, Status, CreatedAt, UpdatedAt
    )
    VALUES
    (
        @CatalogNodeId, N'apparel_accessories', N'Vestuário e acessórios',
        N'vestuario e acessorios',
        N'Nó canônico baseado na categoria Google 166: Vestuário e acessórios.',
        1, N'Catálogo > Vestuário e acessórios',
        @ApparelAndAccessoriesGoogleCategoryId, N'Google', N'Active', @Now, @Now
    );

    SET @ApparelAndAccessoriesNodeId = CONVERT(BIGINT, SCOPE_IDENTITY());

    INSERT INTO Catalog.CanonicalTaxonomyNodes
    (
        ParentId, Code, Name, NormalizedName, Description, Depth, Path,
        GoogleCategoryId, Source, Status, CreatedAt, UpdatedAt
    )
    VALUES
    (
        @ApparelAndAccessoriesNodeId, N'shoes', N'Sapatos', N'sapatos',
        N'Nó canônico baseado na categoria Google 187: Vestuário e acessórios > Sapatos.',
        2, N'Catálogo > Vestuário e acessórios > Sapatos',
        @ShoesGoogleCategoryId, N'Google', N'Active', @Now, @Now
    );

    /* Segment mappings remain empty until AI/catalog-engineering review. */
    IF EXISTS (SELECT 1 FROM Catalog.CanonicalTaxonomyNodeSegmentDefinitions)
        THROW 51037, 'Starter taxonomy must not contain segment mappings before review.', 1;

    IF (SELECT COUNT(*) FROM Catalog.CanonicalTaxonomyNodes) <> 3
        THROW 51038, 'Canonical taxonomy starter tree must contain exactly three nodes.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Catalog.CanonicalTaxonomyNodes AS Shoes
        INNER JOIN Catalog.CanonicalTaxonomyNodes AS ApparelAndAccessories
            ON ApparelAndAccessories.CanonicalTaxonomyNodeId = Shoes.ParentId
        INNER JOIN Catalog.CanonicalTaxonomyNodes AS CatalogRoot
            ON CatalogRoot.CanonicalTaxonomyNodeId = ApparelAndAccessories.ParentId
        WHERE CatalogRoot.Code = N'catalog'
          AND CatalogRoot.Name = N'Catálogo'
          AND CatalogRoot.Depth = 0
          AND CatalogRoot.Path = N'Catálogo'
          AND CatalogRoot.GoogleCategoryId IS NULL
          AND CatalogRoot.Source = N'Yunu'
          AND CatalogRoot.Status = N'Active'
          AND ApparelAndAccessories.Code = N'apparel_accessories'
          AND ApparelAndAccessories.Name = N'Vestuário e acessórios'
          AND ApparelAndAccessories.Depth = 1
          AND ApparelAndAccessories.Path = N'Catálogo > Vestuário e acessórios'
          AND ApparelAndAccessories.GoogleCategoryId = @ApparelAndAccessoriesGoogleCategoryId
          AND ApparelAndAccessories.Source = N'Google'
          AND ApparelAndAccessories.Status = N'Active'
          AND Shoes.Code = N'shoes'
          AND Shoes.Name = N'Sapatos'
          AND Shoes.Depth = 2
          AND Shoes.Path = N'Catálogo > Vestuário e acessórios > Sapatos'
          AND Shoes.GoogleCategoryId = @ShoesGoogleCategoryId
          AND Shoes.Source = N'Google'
          AND Shoes.Status = N'Active'
    )
        THROW 51039, 'Canonical taxonomy starter hierarchy failed validation.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

/* Final node verification. */
SELECT
    Node.CanonicalTaxonomyNodeId,
    Node.ParentId,
    Node.Code,
    Node.Name,
    Node.NormalizedName,
    Node.Description,
    Node.Depth,
    Node.Path,
    Node.GoogleCategoryId,
    GoogleCategory.FullPath AS GoogleCategoryFullPath,
    Node.Source,
    Node.Status,
    Node.CreatedAt,
    Node.UpdatedAt
FROM Catalog.CanonicalTaxonomyNodes AS Node
LEFT JOIN Catalog.GoogleTaxonomyCategories AS GoogleCategory
    ON GoogleCategory.GoogleCategoryId = Node.GoogleCategoryId
ORDER BY Node.Depth, Node.CanonicalTaxonomyNodeId;

/* Empty until AI/Yunu segment review starts. */
SELECT
    Association.CanonicalTaxonomyNodeId,
    Node.Path AS CanonicalNodePath,
    Association.SegmentDefinitionId,
    Definition.Code AS SegmentCode,
    Definition.Name AS SegmentName,
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
ORDER BY Node.Depth, Node.CanonicalTaxonomyNodeId, Definition.Code;
