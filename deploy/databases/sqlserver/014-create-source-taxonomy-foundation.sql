/*
    Yunu.Commerce - Source Taxonomy Foundation (Phase 1: SQL Server schema only)
    Target: SQL Server 2022+

    Decision (docs/adr/0014-provider-neutral-source-taxonomy.md):
      SourceTaxonomy is a provider-neutral Anti-Corruption Layer between
      provider-native taxonomies (Google, Mercado Livre, Amazon, eBay,
      Shopify, Walmart, client PIM/ERP/catalog trees, and providers not yet
      known today) and the future generic semantic/CanonicalTaxonomy
      resolution pipeline:

        Provider-native taxonomy
              -> provider adapter
              -> SourceTaxonomy
              -> generic semantic resolver
              -> CanonicalTaxonomy proposal/resolution

    This migration creates SQL Server storage ONLY:
      - Catalog.SourceTaxonomies
      - Catalog.SourceTaxonomyNodes
      - Integration.SourceTaxonomyImports

    Explicitly NOT done here (out of scope for this phase):
      - no C# domain/application/infrastructure code;
      - no adapters (Google, Mercado Livre, ...);
      - no relationship to Catalog.CanonicalTaxonomyNodes;
      - no relationship to Catalog.GoogleTaxonomyCategories /
        Integration.GoogleTaxonomyImports, both of which remain untouched;
      - no provider-specific column anywhere in the new tables
        (ProviderCode/ScopeCode/ExternalTaxonomyId/ExternalNodeId/AdapterCode
        are the only, intentionally generic, identity surfaces);
      - no CanonicalTaxonomyRootTopologyPolicy (docs/adr/0013) applied to
        SourceTaxonomy: multiple roots per SourceTaxonomy are supported by
        design (ADR-0014 §12);
      - no closed NodeType or Status state machine: both remain open,
        extensible strings so future providers/lifecycles are not blocked by
        a SQL CHECK enumeration.

    Same-taxonomy parent enforcement (ADR-0014 §4/§7):
      A naive self-referencing FK on SourceTaxonomyNodeId alone would allow a
      node belonging to one SourceTaxonomy to reference a parent belonging to
      a different SourceTaxonomy. This is prevented here using a composite
      FK: (SourceTaxonomyId, ParentSourceTaxonomyNodeId) references a unique
      key on (SourceTaxonomyId, SourceTaxonomyNodeId), so SQL Server rejects
      any cross-taxonomy parent assignment at the database level.

    The script is idempotent: every object is guarded by an existence check
    against sys.schemas / sys.tables / sys.indexes / sys.check_constraints /
    sys.foreign_keys / sys.key_constraints, so it can be safely re-executed
    without dropping or recreating existing objects or data.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF SCHEMA_ID(N'Catalog') IS NULL
        EXEC(N'CREATE SCHEMA Catalog AUTHORIZATION dbo;');

    IF SCHEMA_ID(N'Integration') IS NULL
        EXEC(N'CREATE SCHEMA Integration AUTHORIZATION dbo;');

    /* ================================================================
       Catalog.SourceTaxonomies
       Provider-neutral taxonomy descriptor/header.
       ================================================================ */
    IF OBJECT_ID(N'Catalog.SourceTaxonomies', N'U') IS NULL
    BEGIN
        CREATE TABLE Catalog.SourceTaxonomies
        (
            SourceTaxonomyId   BIGINT IDENTITY(1, 1) NOT NULL,
            Code               NVARCHAR(120)  NOT NULL,
            Name               NVARCHAR(250)  NOT NULL,
            ProviderCode       NVARCHAR(80)   NOT NULL,
            ScopeCode          NVARCHAR(120)  NULL,
            ExternalTaxonomyId NVARCHAR(200)  NULL,
            ExternalVersion    NVARCHAR(200)  NULL,
            DefaultLanguage    NVARCHAR(10)   NOT NULL,
            SourceUri          NVARCHAR(1000) NULL,
            SourceChecksum     NVARCHAR(128)  NULL,
            IsActive           BIT NOT NULL
                CONSTRAINT DF_SourceTaxonomies_IsActive DEFAULT (1),
            CreatedAt          DATETIME2(7) NOT NULL
                CONSTRAINT DF_SourceTaxonomies_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt          DATETIME2(7) NULL,
            ImportedAt         DATETIME2(7) NOT NULL
                CONSTRAINT DF_SourceTaxonomies_ImportedAt DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_SourceTaxonomies
                PRIMARY KEY CLUSTERED (SourceTaxonomyId),

            CONSTRAINT UQ_SourceTaxonomies_Code
                UNIQUE (Code),

            CONSTRAINT CK_SourceTaxonomies_Code_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Code))) > 0),

            CONSTRAINT CK_SourceTaxonomies_Name_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Name))) > 0),

            CONSTRAINT CK_SourceTaxonomies_ProviderCode_NotBlank
                CHECK (LEN(LTRIM(RTRIM(ProviderCode))) > 0),

            CONSTRAINT CK_SourceTaxonomies_DefaultLanguage_NotBlank
                CHECK (LEN(LTRIM(RTRIM(DefaultLanguage))) > 0)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SourceTaxonomies')
          AND name = N'IX_SourceTaxonomies_ProviderCode_ScopeCode'
    )
        CREATE INDEX IX_SourceTaxonomies_ProviderCode_ScopeCode
            ON Catalog.SourceTaxonomies (ProviderCode, ScopeCode)
            INCLUDE (Code, Name, IsActive);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SourceTaxonomies')
          AND name = N'IX_SourceTaxonomies_IsActive'
    )
        CREATE INDEX IX_SourceTaxonomies_IsActive
            ON Catalog.SourceTaxonomies (IsActive);

    /* ================================================================
       Catalog.SourceTaxonomyNodes
       Normalized provider-neutral taxonomy tree.
       ================================================================ */
    IF OBJECT_ID(N'Catalog.SourceTaxonomyNodes', N'U') IS NULL
    BEGIN
        CREATE TABLE Catalog.SourceTaxonomyNodes
        (
            SourceTaxonomyNodeId       BIGINT IDENTITY(1, 1) NOT NULL,
            SourceTaxonomyId           BIGINT NOT NULL,
            ExternalNodeId             NVARCHAR(200) NOT NULL,
            ParentSourceTaxonomyNodeId BIGINT NULL,
            NodeType                   NVARCHAR(50)  NOT NULL,
            Name                       NVARCHAR(300) NOT NULL,
            FullPath                   NVARCHAR(2000) NOT NULL,
            Level                      INT NOT NULL,
            IsLeaf                     BIT NOT NULL
                CONSTRAINT DF_SourceTaxonomyNodes_IsLeaf DEFAULT (0),
            IsActive                   BIT NOT NULL
                CONSTRAINT DF_SourceTaxonomyNodes_IsActive DEFAULT (1),
            SourceLanguage             NVARCHAR(10) NOT NULL,
            CreatedAt                  DATETIME2(7) NOT NULL
                CONSTRAINT DF_SourceTaxonomyNodes_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt                  DATETIME2(7) NULL,
            ImportedAt                 DATETIME2(7) NOT NULL
                CONSTRAINT DF_SourceTaxonomyNodes_ImportedAt DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_SourceTaxonomyNodes
                PRIMARY KEY CLUSTERED (SourceTaxonomyNodeId),

            CONSTRAINT UQ_SourceTaxonomyNodes_SourceTaxonomyId_ExternalNodeId
                UNIQUE (SourceTaxonomyId, ExternalNodeId),

            CONSTRAINT FK_SourceTaxonomyNodes_SourceTaxonomy
                FOREIGN KEY (SourceTaxonomyId)
                REFERENCES Catalog.SourceTaxonomies (SourceTaxonomyId),
                -- No cascade delete: SourceTaxonomy rows are deactivated, never deleted.

            CONSTRAINT CK_SourceTaxonomyNodes_ExternalNodeId_NotBlank
                CHECK (LEN(LTRIM(RTRIM(ExternalNodeId))) > 0),

            CONSTRAINT CK_SourceTaxonomyNodes_NodeType_NotBlank
                CHECK (LEN(LTRIM(RTRIM(NodeType))) > 0),

            CONSTRAINT CK_SourceTaxonomyNodes_Name_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Name))) > 0),

            CONSTRAINT CK_SourceTaxonomyNodes_FullPath_NotBlank
                CHECK (LEN(LTRIM(RTRIM(FullPath))) > 0),

            CONSTRAINT CK_SourceTaxonomyNodes_SourceLanguage_NotBlank
                CHECK (LEN(LTRIM(RTRIM(SourceLanguage))) > 0),

            CONSTRAINT CK_SourceTaxonomyNodes_Level_NotNegative
                CHECK (Level >= 0),

            CONSTRAINT CK_SourceTaxonomyNodes_NotSelfParent
                CHECK
                (
                    ParentSourceTaxonomyNodeId IS NULL
                    OR ParentSourceTaxonomyNodeId <> SourceTaxonomyNodeId
                )
        );

        /* Supporting unique key required so the composite same-taxonomy
           parent FK below can reference (SourceTaxonomyId, SourceTaxonomyNodeId). */
        CREATE UNIQUE INDEX UX_SourceTaxonomyNodes_SourceTaxonomyId_SourceTaxonomyNodeId
            ON Catalog.SourceTaxonomyNodes (SourceTaxonomyId, SourceTaxonomyNodeId);
    END;

    /* Composite FK enforcing that a node's parent must belong to the SAME
       SourceTaxonomy. Added as a separate guarded step so it can be applied
       even if the table already existed from a prior partial run. */
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_SourceTaxonomyNodes_Parent_SameTaxonomy'
          AND parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
    )
        ALTER TABLE Catalog.SourceTaxonomyNodes WITH CHECK
            ADD CONSTRAINT FK_SourceTaxonomyNodes_Parent_SameTaxonomy
                FOREIGN KEY (SourceTaxonomyId, ParentSourceTaxonomyNodeId)
                REFERENCES Catalog.SourceTaxonomyNodes (SourceTaxonomyId, SourceTaxonomyNodeId);
                -- No cascade delete: taxonomy nodes are deactivated, never deleted.

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
          AND name = N'IX_SourceTaxonomyNodes_SourceTaxonomyId_Parent'
    )
        CREATE INDEX IX_SourceTaxonomyNodes_SourceTaxonomyId_Parent
            ON Catalog.SourceTaxonomyNodes (SourceTaxonomyId, ParentSourceTaxonomyNodeId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
          AND name = N'IX_SourceTaxonomyNodes_SourceTaxonomyId_IsActive'
    )
        CREATE INDEX IX_SourceTaxonomyNodes_SourceTaxonomyId_IsActive
            ON Catalog.SourceTaxonomyNodes (SourceTaxonomyId, IsActive)
            INCLUDE (ExternalNodeId, Name, NodeType, Level);

    /* ================================================================
       Integration.SourceTaxonomyImports
       Generic SourceTaxonomy import history.
       ================================================================ */
    IF OBJECT_ID(N'Integration.SourceTaxonomyImports', N'U') IS NULL
    BEGIN
        CREATE TABLE Integration.SourceTaxonomyImports
        (
            ImportId         BIGINT IDENTITY(1, 1) NOT NULL,
            SourceTaxonomyId BIGINT NOT NULL,
            AdapterCode      NVARCHAR(80)   NOT NULL,
            SourceUri        NVARCHAR(1000) NULL,
            ExternalVersion  NVARCHAR(200)  NULL,
            SourceChecksum   NVARCHAR(128)  NULL,
            StartedAt        DATETIME2(7) NOT NULL,
            CompletedAt      DATETIME2(7) NULL,
            NodeCount        INT NOT NULL
                CONSTRAINT DF_SourceTaxonomyImports_NodeCount DEFAULT (0),
            InsertedCount    INT NOT NULL
                CONSTRAINT DF_SourceTaxonomyImports_InsertedCount DEFAULT (0),
            UpdatedCount     INT NOT NULL
                CONSTRAINT DF_SourceTaxonomyImports_UpdatedCount DEFAULT (0),
            DeactivatedCount INT NOT NULL
                CONSTRAINT DF_SourceTaxonomyImports_DeactivatedCount DEFAULT (0),
            Status           NVARCHAR(30) NOT NULL,
            ErrorMessage     NVARCHAR(2000) NULL,

            CONSTRAINT PK_SourceTaxonomyImports
                PRIMARY KEY CLUSTERED (ImportId),

            CONSTRAINT FK_SourceTaxonomyImports_SourceTaxonomy
                FOREIGN KEY (SourceTaxonomyId)
                REFERENCES Catalog.SourceTaxonomies (SourceTaxonomyId),
                -- No cascade delete: SourceTaxonomy rows are deactivated, never deleted.

            CONSTRAINT CK_SourceTaxonomyImports_AdapterCode_NotBlank
                CHECK (LEN(LTRIM(RTRIM(AdapterCode))) > 0),

            CONSTRAINT CK_SourceTaxonomyImports_Status_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Status))) > 0),

            CONSTRAINT CK_SourceTaxonomyImports_NodeCount_NotNegative
                CHECK (NodeCount >= 0),

            CONSTRAINT CK_SourceTaxonomyImports_InsertedCount_NotNegative
                CHECK (InsertedCount >= 0),

            CONSTRAINT CK_SourceTaxonomyImports_UpdatedCount_NotNegative
                CHECK (UpdatedCount >= 0),

            CONSTRAINT CK_SourceTaxonomyImports_DeactivatedCount_NotNegative
                CHECK (DeactivatedCount >= 0)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'Integration.SourceTaxonomyImports')
          AND name = N'IX_SourceTaxonomyImports_SourceTaxonomyId_StartedAt'
    )
        CREATE INDEX IX_SourceTaxonomyImports_SourceTaxonomyId_StartedAt
            ON Integration.SourceTaxonomyImports (SourceTaxonomyId, StartedAt);

    /* ================================================================
       Post-migration structural validation.
       ================================================================ */
    IF OBJECT_ID(N'Catalog.SourceTaxonomies', N'U') IS NULL
        THROW 51090, 'Catalog.SourceTaxonomies does not exist after migration.', 1;

    IF OBJECT_ID(N'Catalog.SourceTaxonomyNodes', N'U') IS NULL
        THROW 51091, 'Catalog.SourceTaxonomyNodes does not exist after migration.', 1;

    IF OBJECT_ID(N'Integration.SourceTaxonomyImports', N'U') IS NULL
        THROW 51092, 'Integration.SourceTaxonomyImports does not exist after migration.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomies')
          AND type = 'PK'
    )
        THROW 51093, 'Catalog.SourceTaxonomies is missing its primary key.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
          AND type = 'PK'
    )
        THROW 51094, 'Catalog.SourceTaxonomyNodes is missing its primary key.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'Integration.SourceTaxonomyImports')
          AND type = 'PK'
    )
        THROW 51095, 'Integration.SourceTaxonomyImports is missing its primary key.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomies')
          AND name = N'UQ_SourceTaxonomies_Code'
    )
        THROW 51096, 'Catalog.SourceTaxonomies.Code uniqueness constraint is missing.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
          AND name = N'UQ_SourceTaxonomyNodes_SourceTaxonomyId_ExternalNodeId'
    )
        THROW 51097, 'Catalog.SourceTaxonomyNodes UNIQUE(SourceTaxonomyId, ExternalNodeId) is missing.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_SourceTaxonomyNodes_SourceTaxonomy'
          AND parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
    )
        THROW 51098, 'Catalog.SourceTaxonomyNodes.SourceTaxonomyId FK is missing.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_SourceTaxonomyNodes_Parent_SameTaxonomy'
          AND parent_object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
    )
        THROW 51099, 'Catalog.SourceTaxonomyNodes same-taxonomy parent FK is missing.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_SourceTaxonomyImports_SourceTaxonomy'
          AND parent_object_id = OBJECT_ID(N'Integration.SourceTaxonomyImports')
    )
        THROW 51100, 'Integration.SourceTaxonomyImports.SourceTaxonomyId FK is missing.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id IN
        (
            OBJECT_ID(N'Catalog.SourceTaxonomyNodes'),
            OBJECT_ID(N'Integration.SourceTaxonomyImports')
        )
        AND delete_referential_action <> 0
    )
        THROW 51101, 'A SourceTaxonomy foreign key unexpectedly configures a delete action other than NO ACTION.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
          AND c.name = N'ExternalNodeId'
          AND t.name IN (N'nvarchar', N'varchar', N'nchar', N'char')
    )
        THROW 51102, 'Catalog.SourceTaxonomyNodes.ExternalNodeId must be a character/string type.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
          AND name = N'ParentSourceTaxonomyNodeId'
          AND is_nullable = 0
    )
        THROW 51103, 'Catalog.SourceTaxonomyNodes.ParentSourceTaxonomyNodeId must remain nullable.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns c
        WHERE c.object_id IN
        (
            OBJECT_ID(N'Catalog.SourceTaxonomies'),
            OBJECT_ID(N'Catalog.SourceTaxonomyNodes'),
            OBJECT_ID(N'Integration.SourceTaxonomyImports')
        )
        AND c.name IN
        (
            N'GoogleCategoryId', N'ParentGoogleCategoryId',
            N'MercadoLivreCategoryId', N'MarketplaceId', N'SiteId',
            N'AmazonBrowseNodeId', N'AmazonMarketplaceId',
            N'EbayCategoryId', N'ShopifyCategoryId',
            N'WalmartProductTypeId', N'ProviderPayloadJson',
            N'CanonicalTaxonomyNodeId'
        )
    )
        THROW 51104, 'A provider-specific or CanonicalTaxonomy-coupled column was unexpectedly found on a SourceTaxonomy table.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
