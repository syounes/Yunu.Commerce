/*
    Yunu.Commerce - Canonical Taxonomy and Segmentation
    Target: SQL Server 2022+

    Final model decisions:
      - A canonical taxonomy node has zero or one directly assigned SegmentDefinition.
      - A SegmentDefinition may be reused by many nodes.
      - A SegmentDefinition has one or many SegmentOptions (enforced by domain/application).
      - Effective segmentation is obtained by traversing the node ancestors.
      - Child nodes may define an additional segmentation of their own.
      - IsRoot, IsLeaf, IsAssignable, HasSegment and AppliesToDescendants are derived,
        therefore they are intentionally not persisted.
      - Structure and stable codes are in English; business content is in pt-BR.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF SCHEMA_ID(N'Catalog') IS NULL
        EXEC(N'CREATE SCHEMA Catalog AUTHORIZATION dbo;');

    /* ================================================================
       SegmentDefinitions
       Created first because CanonicalTaxonomyNodes references it.
       ================================================================ */
    IF OBJECT_ID(N'Catalog.SegmentDefinitions', N'U') IS NULL
    BEGIN
        CREATE TABLE Catalog.SegmentDefinitions
        (
            SegmentDefinitionId BIGINT IDENTITY(1, 1) NOT NULL,
            Code                NVARCHAR(100) NOT NULL,
            Name                NVARCHAR(200) NOT NULL,
            NormalizedName      NVARCHAR(200) NOT NULL,
            Description         NVARCHAR(1000) NULL,
            SemanticText        NVARCHAR(2000) NULL,
            SelectionMode       NVARCHAR(16) NOT NULL,
            IsRequired          BIT NOT NULL
                CONSTRAINT DF_SegmentDefinitions_IsRequired DEFAULT (0),
            Status              NVARCHAR(16) NOT NULL
                CONSTRAINT DF_SegmentDefinitions_Status DEFAULT (N'Draft'),
            CreatedAt           DATETIME2(7) NOT NULL
                CONSTRAINT DF_SegmentDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL
                CONSTRAINT DF_SegmentDefinitions_UpdatedAt DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_SegmentDefinitions
                PRIMARY KEY CLUSTERED (SegmentDefinitionId),

            CONSTRAINT UQ_SegmentDefinitions_Code
                UNIQUE (Code),

            CONSTRAINT CK_SegmentDefinitions_Code_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Code))) > 0),

            CONSTRAINT CK_SegmentDefinitions_Name_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Name))) > 0),

            CONSTRAINT CK_SegmentDefinitions_NormalizedName_NotBlank
                CHECK (LEN(LTRIM(RTRIM(NormalizedName))) > 0),

            CONSTRAINT CK_SegmentDefinitions_SelectionMode
                CHECK (SelectionMode IN (N'Single', N'Multiple')),

            CONSTRAINT CK_SegmentDefinitions_Status
                CHECK (Status IN (N'Draft', N'Active', N'Inactive', N'Archived')),

            CONSTRAINT CK_SegmentDefinitions_Dates
                CHECK (UpdatedAt >= CreatedAt)
        );

        CREATE INDEX IX_SegmentDefinitions_Status
            ON Catalog.SegmentDefinitions (Status)
            INCLUDE (Code, Name, SelectionMode, IsRequired);

        CREATE INDEX IX_SegmentDefinitions_NormalizedName
            ON Catalog.SegmentDefinitions (NormalizedName);
    END;

    /* ================================================================
       CanonicalTaxonomyNodes
       ParentId is self-referencing. SegmentDefinitionId is optional.
       ================================================================ */
    IF OBJECT_ID(N'Catalog.CanonicalTaxonomyNodes', N'U') IS NULL
    BEGIN
        CREATE TABLE Catalog.CanonicalTaxonomyNodes
        (
            CanonicalTaxonomyNodeId BIGINT IDENTITY(1, 1) NOT NULL,
            ParentId                BIGINT NULL,
            SegmentDefinitionId     BIGINT NULL,
            Code                    NVARCHAR(120) NOT NULL,
            Name                    NVARCHAR(250) NOT NULL,
            NormalizedName          NVARCHAR(250) NOT NULL,
            Description             NVARCHAR(1000) NULL,
            Depth                   SMALLINT NOT NULL,
            Path                    NVARCHAR(2000) NOT NULL,
            GoogleCategoryId        BIGINT NULL,
            Source                  NVARCHAR(16) NOT NULL
                CONSTRAINT DF_CanonicalTaxonomyNodes_Source DEFAULT (N'Yunu'),
            Status                  NVARCHAR(16) NOT NULL
                CONSTRAINT DF_CanonicalTaxonomyNodes_Status DEFAULT (N'Draft'),
            CreatedAt               DATETIME2(7) NOT NULL
                CONSTRAINT DF_CanonicalTaxonomyNodes_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt               DATETIME2(7) NOT NULL
                CONSTRAINT DF_CanonicalTaxonomyNodes_UpdatedAt DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_CanonicalTaxonomyNodes
                PRIMARY KEY CLUSTERED (CanonicalTaxonomyNodeId),

            CONSTRAINT UQ_CanonicalTaxonomyNodes_Code
                UNIQUE (Code),

            CONSTRAINT UQ_CanonicalTaxonomyNodes_Path
                UNIQUE (Path),

            CONSTRAINT FK_CanonicalTaxonomyNodes_Parent
                FOREIGN KEY (ParentId)
                REFERENCES Catalog.CanonicalTaxonomyNodes (CanonicalTaxonomyNodeId),

            CONSTRAINT FK_CanonicalTaxonomyNodes_SegmentDefinition
                FOREIGN KEY (SegmentDefinitionId)
                REFERENCES Catalog.SegmentDefinitions (SegmentDefinitionId),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Code_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Code))) > 0),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Name_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Name))) > 0),

            CONSTRAINT CK_CanonicalTaxonomyNodes_NormalizedName_NotBlank
                CHECK (LEN(LTRIM(RTRIM(NormalizedName))) > 0),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Path_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Path))) > 0),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Depth
                CHECK
                (
                    (ParentId IS NULL AND Depth = 0)
                    OR
                    (ParentId IS NOT NULL AND Depth > 0)
                ),

            CONSTRAINT CK_CanonicalTaxonomyNodes_NotSelfParent
                CHECK
                (
                    ParentId IS NULL
                    OR ParentId <> CanonicalTaxonomyNodeId
                ),

            CONSTRAINT CK_CanonicalTaxonomyNodes_GoogleCategoryId
                CHECK (GoogleCategoryId IS NULL OR GoogleCategoryId > 0),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Source
                CHECK (Source IN (N'Yunu', N'Google', N'Client')),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Status
                CHECK (Status IN (N'Draft', N'Active', N'Inactive', N'Archived')),

            CONSTRAINT CK_CanonicalTaxonomyNodes_Dates
                CHECK (UpdatedAt >= CreatedAt)
        );

        CREATE INDEX IX_CanonicalTaxonomyNodes_ParentId_Status
            ON Catalog.CanonicalTaxonomyNodes (ParentId, Status)
            INCLUDE (Code, Name, Depth, Path, SegmentDefinitionId);

        CREATE INDEX IX_CanonicalTaxonomyNodes_SegmentDefinitionId
            ON Catalog.CanonicalTaxonomyNodes (SegmentDefinitionId)
            WHERE SegmentDefinitionId IS NOT NULL;

        CREATE INDEX IX_CanonicalTaxonomyNodes_NormalizedName_Status
            ON Catalog.CanonicalTaxonomyNodes (NormalizedName, Status)
            INCLUDE (Code, Path, GoogleCategoryId);

        CREATE UNIQUE INDEX UX_CanonicalTaxonomyNodes_GoogleCategoryId
            ON Catalog.CanonicalTaxonomyNodes (GoogleCategoryId)
            WHERE GoogleCategoryId IS NOT NULL;
    END;

    /* ================================================================
       SegmentOptions
       ================================================================ */
    IF OBJECT_ID(N'Catalog.SegmentOptions', N'U') IS NULL
    BEGIN
        CREATE TABLE Catalog.SegmentOptions
        (
            SegmentOptionId     BIGINT IDENTITY(1, 1) NOT NULL,
            SegmentDefinitionId BIGINT NOT NULL,
            Code                NVARCHAR(100) NOT NULL,
            Name                NVARCHAR(200) NOT NULL,
            NormalizedName      NVARCHAR(200) NOT NULL,
            Description         NVARCHAR(1000) NULL,
            SemanticText        NVARCHAR(2000) NULL,
            DisplayOrder        INT NOT NULL
                CONSTRAINT DF_SegmentOptions_DisplayOrder DEFAULT (0),
            Status              NVARCHAR(16) NOT NULL
                CONSTRAINT DF_SegmentOptions_Status DEFAULT (N'Draft'),
            CreatedAt           DATETIME2(7) NOT NULL
                CONSTRAINT DF_SegmentOptions_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL
                CONSTRAINT DF_SegmentOptions_UpdatedAt DEFAULT (SYSUTCDATETIME()),

            CONSTRAINT PK_SegmentOptions
                PRIMARY KEY CLUSTERED (SegmentOptionId),

            CONSTRAINT FK_SegmentOptions_SegmentDefinition
                FOREIGN KEY (SegmentDefinitionId)
                REFERENCES Catalog.SegmentDefinitions (SegmentDefinitionId),

            CONSTRAINT UQ_SegmentOptions_Definition_Code
                UNIQUE (SegmentDefinitionId, Code),

            CONSTRAINT UQ_SegmentOptions_Definition_NormalizedName
                UNIQUE (SegmentDefinitionId, NormalizedName),

            CONSTRAINT CK_SegmentOptions_Code_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Code))) > 0),

            CONSTRAINT CK_SegmentOptions_Name_NotBlank
                CHECK (LEN(LTRIM(RTRIM(Name))) > 0),

            CONSTRAINT CK_SegmentOptions_NormalizedName_NotBlank
                CHECK (LEN(LTRIM(RTRIM(NormalizedName))) > 0),

            CONSTRAINT CK_SegmentOptions_DisplayOrder
                CHECK (DisplayOrder >= 0),

            CONSTRAINT CK_SegmentOptions_Status
                CHECK (Status IN (N'Draft', N'Active', N'Inactive', N'Archived')),

            CONSTRAINT CK_SegmentOptions_Dates
                CHECK (UpdatedAt >= CreatedAt)
        );

        CREATE INDEX IX_SegmentOptions_Definition_Status_DisplayOrder
            ON Catalog.SegmentOptions
            (
                SegmentDefinitionId,
                Status,
                DisplayOrder
            )
            INCLUDE (Code, Name, SemanticText);
    END;

    /* ================================================================
       Initial SegmentDefinitions (idempotent seed)
       Codes are canonical and immutable after publication.
       ================================================================ */
    DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

    DECLARE @SegmentDefinitions TABLE
    (
        Code           NVARCHAR(100) NOT NULL,
        Name           NVARCHAR(200) NOT NULL,
        NormalizedName NVARCHAR(200) NOT NULL,
        Description    NVARCHAR(1000) NULL,
        SemanticText   NVARCHAR(2000) NULL,
        SelectionMode  NVARCHAR(16) NOT NULL,
        IsRequired     BIT NOT NULL,
        Status         NVARCHAR(16) NOT NULL
    );

    INSERT INTO @SegmentDefinitions
    (
        Code, Name, NormalizedName, Description, SemanticText,
        SelectionMode, IsRequired, Status
    )
    VALUES
    (
        N'target_audience',
        N'Público-alvo',
        N'publico alvo',
        N'Segmenta os produtos pela faixa etária ou estágio de vida do consumidor a que se destinam.',
        N'Definição de segmentação do público-alvo do produto. Identifica para quem o item foi projetado ou recomendado, considerando adulto, adolescente, infantil ou bebê. Use evidências explícitas no título, descrição, categoria e atributos. Não inferir idade apenas por cor, estampa ou tamanho quando não houver contexto suficiente.',
        N'Single',
        1,
        N'Active'
    ),
    (
        N'gender',
        N'Gênero',
        N'genero',
        N'Segmenta produtos pelo gênero indicado pelo fabricante ou pela proposta comercial do item.',
        N'Definição de segmentação de gênero do produto. Identifica masculino, feminino ou unissex a partir de evidências explícitas do fabricante, nome, descrição e atributos. Não deduzir gênero somente por cor, formato ou imagem. Quando o produto atender a mais de um gênero sem distinção, utilizar unissex.',
        N'Single',
        0,
        N'Active'
    ),
    (
        N'sport_modality',
        N'Modalidade esportiva',
        N'modalidade esportiva',
        N'Segmenta artigos esportivos de acordo com a modalidade principal de uso.',
        N'Definição de segmentação da modalidade esportiva principal do produto. Diferencia corrida, futebol, treino e academia, basquete e tênis como esporte. Considere finalidade, construção, tecnologias, superfície de uso e indicação do fabricante. A palavra tênis pode significar calçado ou modalidade esportiva; exija contexto para selecionar a modalidade.',
        N'Single',
        1,
        N'Active'
    ),
    (
        N'foot_pronation',
        N'Tipo de pisada',
        N'tipo de pisada',
        N'Segmenta calçados de corrida conforme o suporte biomecânico indicado para a pisada.',
        N'Definição de segmentação do tipo de pisada suportado pelo calçado de corrida. Identifica pisada neutra, pronada ou supinada somente quando houver indicação explícita, especificação técnica ou tecnologia de suporte correspondente. Não inferir o tipo de pisada apenas pela aparência do solado.',
        N'Single',
        0,
        N'Active'
    ),
    (
        N'computer_profile',
        N'Perfil de uso do computador',
        N'perfil de uso do computador',
        N'Segmenta computadores e notebooks conforme o principal perfil de utilização.',
        N'Definição de segmentação do perfil principal de uso de computadores e notebooks. Diferencia jogos, trabalho corporativo, uso doméstico e estudos, e criação de conteúdo. Considere processador, memória RAM, GPU, armazenamento, tela, autonomia e descrição comercial. Não classificar como gamer somente por iluminação RGB.',
        N'Single',
        0,
        N'Active'
    );

    MERGE Catalog.SegmentDefinitions WITH (HOLDLOCK) AS Target
    USING @SegmentDefinitions AS Source
       ON Target.Code = Source.Code
    WHEN MATCHED THEN
        UPDATE SET
            Name           = Source.Name,
            NormalizedName = Source.NormalizedName,
            Description    = Source.Description,
            SemanticText   = Source.SemanticText,
            SelectionMode  = Source.SelectionMode,
            IsRequired     = Source.IsRequired,
            Status         = Source.Status,
            UpdatedAt      = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            Code, Name, NormalizedName, Description, SemanticText,
            SelectionMode, IsRequired, Status, CreatedAt, UpdatedAt
        )
        VALUES
        (
            Source.Code, Source.Name, Source.NormalizedName,
            Source.Description, Source.SemanticText, Source.SelectionMode,
            Source.IsRequired, Source.Status, @Now, @Now
        );

    /* ================================================================
       Initial SegmentOptions (idempotent seed)
       ================================================================ */
    DECLARE @SegmentOptions TABLE
    (
        DefinitionCode NVARCHAR(100) NOT NULL,
        Code           NVARCHAR(100) NOT NULL,
        Name           NVARCHAR(200) NOT NULL,
        NormalizedName NVARCHAR(200) NOT NULL,
        Description    NVARCHAR(1000) NULL,
        SemanticText   NVARCHAR(2000) NULL,
        DisplayOrder   INT NOT NULL,
        Status         NVARCHAR(16) NOT NULL
    );

    INSERT INTO @SegmentOptions
    (
        DefinitionCode, Code, Name, NormalizedName, Description,
        SemanticText, DisplayOrder, Status
    )
    VALUES
    (N'target_audience', N'ADULT', N'Adulto', N'adulto',
     N'Produtos destinados ao público adulto.',
     N'Público-alvo adulto. Termos e conceitos relacionados: adulto, adulta, homem, mulher, tamanho adulto, uso profissional, maiores de idade. Se houver indicação explícita de infantil, juvenil ou bebê, esta opção não se aplica.', 10, N'Active'),
    (N'target_audience', N'TEEN', N'Adolescente', N'adolescente',
     N'Produtos destinados ao público adolescente ou juvenil.',
     N'Público-alvo adolescente ou juvenil. Termos relacionados: adolescente, teen, juvenil, jovem, faixa etária juvenil. Não confundir produto de estilo jovem destinado a adultos com indicação etária juvenil.', 20, N'Active'),
    (N'target_audience', N'KIDS', N'Infantil', N'infantil',
     N'Produtos destinados a crianças.',
     N'Público-alvo infantil. Termos relacionados: infantil, criança, kids, menino, menina, escolar, tamanho infantil. Não utilizar para bebê quando a descrição indicar primeira infância, recém-nascido ou lactente.', 30, N'Active'),
    (N'target_audience', N'BABY', N'Bebê', N'bebe',
     N'Produtos destinados a bebês e primeira infância.',
     N'Público-alvo bebê. Termos relacionados: bebê, baby, recém-nascido, lactente, primeira infância, enxoval, berçário. Priorizar esta opção quando houver indicação inequívoca de bebê.', 40, N'Active'),

    (N'gender', N'MALE', N'Masculino', N'masculino',
     N'Produto indicado explicitamente para o público masculino.',
     N'Gênero masculino. Termos relacionados: masculino, homem, homens, menino quando combinado com público infantil, male, men. Exigir indicação comercial ou técnica; não inferir somente por cor ou aparência.', 10, N'Active'),
    (N'gender', N'FEMALE', N'Feminino', N'feminino',
     N'Produto indicado explicitamente para o público feminino.',
     N'Gênero feminino. Termos relacionados: feminino, mulher, mulheres, menina quando combinado com público infantil, female, women. Exigir indicação comercial ou técnica; não inferir somente por cor ou aparência.', 20, N'Active'),
    (N'gender', N'UNISEX', N'Unissex', N'unissex',
     N'Produto indicado para mais de um gênero sem diferenciação.',
     N'Gênero unissex. Termos relacionados: unissex, sem gênero, gender neutral, para todos, masculino e feminino. Use quando a indicação do fabricante for compartilhada; ausência de gênero não é evidência suficiente por si só.', 30, N'Active'),

    (N'sport_modality', N'RUNNING', N'Corrida', N'corrida',
     N'Produtos desenvolvidos principalmente para corrida.',
     N'Modalidade corrida. Termos relacionados: corrida, running, jogging, maratona, treino de corrida, corredor, amortecimento para corrida, retorno de energia. Não confundir tênis casual com tênis de corrida.', 10, N'Active'),
    (N'sport_modality', N'FOOTBALL', N'Futebol', N'futebol',
     N'Produtos desenvolvidos principalmente para futebol.',
     N'Modalidade futebol. Termos relacionados: futebol, society, futsal, campo, chuteira, trava, gramado, futebol de salão. Use a superfície e o tipo de solado como evidências complementares.', 20, N'Active'),
    (N'sport_modality', N'TRAINING', N'Treino e academia', N'treino e academia',
     N'Produtos destinados a treinamento físico e academia.',
     N'Modalidade treino e academia. Termos relacionados: training, treino, academia, musculação, funcional, cross training, estabilidade lateral. Não selecionar quando o produto tiver indicação principal inequívoca de corrida.', 30, N'Active'),
    (N'sport_modality', N'BASKETBALL', N'Basquete', N'basquete',
     N'Produtos desenvolvidos principalmente para basquete.',
     N'Modalidade basquete. Termos relacionados: basquete, basketball, quadra, pivô, armador, suporte de tornozelo, tração para quadra. Exigir contexto esportivo.', 40, N'Active'),
    (N'sport_modality', N'TENNIS', N'Tênis', N'tenis',
     N'Produtos desenvolvidos para a modalidade esportiva tênis.',
     N'Modalidade esportiva tênis. Termos relacionados: tênis de quadra, tennis, saibro, quadra rápida, raquete, tenista. Atenção: a palavra tênis isolada frequentemente significa calçado; somente selecionar esta opção quando o contexto indicar o esporte.', 50, N'Active'),

    (N'foot_pronation', N'NEUTRAL', N'Neutra', N'neutra',
     N'Calçado indicado para pisada neutra.',
     N'Tipo de pisada neutra. Termos relacionados: pisada neutra, neutral, distribuição equilibrada, suporte neutro. Selecionar somente com evidência técnica ou indicação do fabricante.', 10, N'Active'),
    (N'foot_pronation', N'PRONATED', N'Pronada', N'pronada',
     N'Calçado indicado para pisada pronada.',
     N'Tipo de pisada pronada. Termos relacionados: pronação, pronada, overpronation, controle de estabilidade, suporte medial, motion control. Não inferir apenas pela densidade visual do solado.', 20, N'Active'),
    (N'foot_pronation', N'SUPINATED', N'Supinada', N'supinada',
     N'Calçado indicado para pisada supinada.',
     N'Tipo de pisada supinada. Termos relacionados: supinação, supinada, underpronation, amortecimento para supinador. Selecionar somente quando houver indicação explícita ou técnica.', 30, N'Active'),

    (N'computer_profile', N'GAMING', N'Jogos', N'jogos',
     N'Computador projetado principalmente para jogos.',
     N'Perfil gamer ou jogos. Evidências: GPU dedicada adequada a jogos, alta taxa de atualização, sistema térmico reforçado, processador de desempenho e indicação gamer. Iluminação RGB isoladamente não determina este perfil.', 10, N'Active'),
    (N'computer_profile', N'BUSINESS', N'Corporativo', N'corporativo',
     N'Computador projetado para trabalho profissional e ambientes corporativos.',
     N'Perfil corporativo ou profissional. Evidências: business, empresarial, segurança corporativa, gerenciamento remoto, TPM, durabilidade, garantia no local, docking station e produtividade de escritório.', 20, N'Active'),
    (N'computer_profile', N'HOME_STUDY', N'Casa e estudos', N'casa e estudos',
     N'Computador destinado ao uso doméstico, estudos e tarefas cotidianas.',
     N'Perfil casa e estudos. Evidências: navegação, aulas, estudos, pacote de escritório, videoconferência, entretenimento leve, uso doméstico e configuração de entrada ou intermediária.', 30, N'Active'),
    (N'computer_profile', N'CREATOR', N'Criação de conteúdo', N'criacao de conteudo',
     N'Computador projetado para produção audiovisual e criação digital.',
     N'Perfil criação de conteúdo. Evidências: edição de vídeo, fotografia, design, modelagem 3D, renderização, tela com fidelidade de cores, GPU dedicada, grande quantidade de memória RAM e processador de alto desempenho.', 40, N'Active');

    ;WITH ResolvedOptions AS
    (
        SELECT
            Definition.SegmentDefinitionId,
            Source.Code,
            Source.Name,
            Source.NormalizedName,
            Source.Description,
            Source.SemanticText,
            Source.DisplayOrder,
            Source.Status
        FROM @SegmentOptions AS Source
        INNER JOIN Catalog.SegmentDefinitions AS Definition
            ON Definition.Code = Source.DefinitionCode
    )
    MERGE Catalog.SegmentOptions WITH (HOLDLOCK) AS Target
    USING ResolvedOptions AS Source
       ON Target.SegmentDefinitionId = Source.SegmentDefinitionId
      AND Target.Code = Source.Code
    WHEN MATCHED THEN
        UPDATE SET
            Name           = Source.Name,
            NormalizedName = Source.NormalizedName,
            Description    = Source.Description,
            SemanticText   = Source.SemanticText,
            DisplayOrder   = Source.DisplayOrder,
            Status         = Source.Status,
            UpdatedAt      = @Now
    WHEN NOT MATCHED THEN
        INSERT
        (
            SegmentDefinitionId, Code, Name, NormalizedName,
            Description, SemanticText, DisplayOrder, Status,
            CreatedAt, UpdatedAt
        )
        VALUES
        (
            Source.SegmentDefinitionId, Source.Code, Source.Name,
            Source.NormalizedName, Source.Description, Source.SemanticText,
            Source.DisplayOrder, Source.Status, @Now, @Now
        );

    /* ================================================================
       Initial CanonicalTaxonomyNodes (idempotent seed)

       Example of inherited effective segmentation:
       running_shoes inherits target_audience, gender and sport_modality,
       then adds foot_pronation directly.
       ================================================================ */
    DECLARE @CanonicalNodes TABLE
    (
        ParentCode            NVARCHAR(120) NULL,
        SegmentDefinitionCode NVARCHAR(100) NULL,
        Code                  NVARCHAR(120) NOT NULL,
        Name                  NVARCHAR(250) NOT NULL,
        NormalizedName        NVARCHAR(250) NOT NULL,
        Description           NVARCHAR(1000) NULL,
        Depth                 SMALLINT NOT NULL,
        Path                  NVARCHAR(2000) NOT NULL,
        GoogleCategoryId      BIGINT NULL,
        Source                NVARCHAR(16) NOT NULL,
        Status                NVARCHAR(16) NOT NULL
    );

    INSERT INTO @CanonicalNodes
    (
        ParentCode, SegmentDefinitionCode, Code, Name, NormalizedName,
        Description, Depth, Path, GoogleCategoryId, Source, Status
    )
    VALUES
    (NULL, NULL, N'catalog', N'Catálogo', N'catalogo',
     N'Raiz técnica da taxonomia canônica do Yunu.Commerce.',
     0, N'/catalog', NULL, N'Yunu', N'Active'),

    (N'catalog', N'target_audience', N'fashion', N'Moda', N'moda',
     N'Produtos de vestuário, calçados e acessórios de moda.',
     1, N'/catalog/fashion', NULL, N'Yunu', N'Active'),

    (N'fashion', N'gender', N'clothing', N'Roupas', N'roupas',
     N'Peças de vestuário para diferentes públicos e ocasiões.',
     2, N'/catalog/fashion/clothing', NULL, N'Yunu', N'Active'),

    (N'fashion', N'gender', N'shoes', N'Calçados', N'calcados',
     N'Calçados casuais, sociais e esportivos.',
     2, N'/catalog/fashion/shoes', 187, N'Yunu', N'Active'),

    (N'shoes', N'sport_modality', N'athletic_shoes', N'Calçados esportivos', N'calcados esportivos',
     N'Calçados projetados para práticas esportivas e treinamento físico.',
     3, N'/catalog/fashion/shoes/athletic-shoes', NULL, N'Yunu', N'Active'),

    (N'athletic_shoes', N'foot_pronation', N'running_shoes', N'Tênis de corrida', N'tenis de corrida',
     N'Calçados esportivos desenvolvidos principalmente para corrida.',
     4, N'/catalog/fashion/shoes/athletic-shoes/running-shoes', NULL, N'Yunu', N'Active'),

    (N'catalog', NULL, N'electronics', N'Eletrônicos', N'eletronicos',
     N'Equipamentos e dispositivos eletrônicos de consumo.',
     1, N'/catalog/electronics', NULL, N'Yunu', N'Active'),

    (N'electronics', N'computer_profile', N'computers', N'Computadores', N'computadores',
     N'Computadores pessoais para diferentes perfis de utilização.',
     2, N'/catalog/electronics/computers', NULL, N'Yunu', N'Active'),

    (N'computers', NULL, N'notebooks', N'Notebooks', N'notebooks',
     N'Computadores pessoais portáteis.',
     3, N'/catalog/electronics/computers/notebooks', NULL, N'Yunu', N'Active');

    DECLARE @CurrentDepth SMALLINT = 0;
    DECLARE @MaximumDepth SMALLINT =
        (SELECT MAX(Depth) FROM @CanonicalNodes);

    WHILE @CurrentDepth <= @MaximumDepth
    BEGIN
        ;WITH ResolvedNodes AS
        (
            SELECT
                Parent.CanonicalTaxonomyNodeId AS ParentId,
                Definition.SegmentDefinitionId,
                Source.Code,
                Source.Name,
                Source.NormalizedName,
                Source.Description,
                Source.Depth,
                Source.Path,
                Source.GoogleCategoryId,
                Source.Source,
                Source.Status
            FROM @CanonicalNodes AS Source
            LEFT JOIN Catalog.CanonicalTaxonomyNodes AS Parent
                ON Parent.Code = Source.ParentCode
            LEFT JOIN Catalog.SegmentDefinitions AS Definition
                ON Definition.Code = Source.SegmentDefinitionCode
            WHERE Source.Depth = @CurrentDepth
        )
        MERGE Catalog.CanonicalTaxonomyNodes WITH (HOLDLOCK) AS Target
        USING ResolvedNodes AS Source
           ON Target.Code = Source.Code
        WHEN MATCHED THEN
            UPDATE SET
                ParentId            = Source.ParentId,
                SegmentDefinitionId = Source.SegmentDefinitionId,
                Name                = Source.Name,
                NormalizedName      = Source.NormalizedName,
                Description         = Source.Description,
                Depth               = Source.Depth,
                Path                = Source.Path,
                GoogleCategoryId    = Source.GoogleCategoryId,
                Source              = Source.Source,
                Status              = Source.Status,
                UpdatedAt           = @Now
        WHEN NOT MATCHED THEN
            INSERT
            (
                ParentId, SegmentDefinitionId, Code, Name, NormalizedName,
                Description, Depth, Path, GoogleCategoryId, Source, Status,
                CreatedAt, UpdatedAt
            )
            VALUES
            (
                Source.ParentId, Source.SegmentDefinitionId, Source.Code,
                Source.Name, Source.NormalizedName, Source.Description,
                Source.Depth, Source.Path, Source.GoogleCategoryId,
                Source.Source, Source.Status, @Now, @Now
            );

        SET @CurrentDepth += 1;
    END;

    /* Seed integrity checks. */
    IF EXISTS
    (
        SELECT 1
        FROM @SegmentOptions AS Source
        LEFT JOIN Catalog.SegmentDefinitions AS Definition
            ON Definition.Code = Source.DefinitionCode
        WHERE Definition.SegmentDefinitionId IS NULL
    )
        THROW 51000, 'A SegmentOption seed references an unknown SegmentDefinition.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @CanonicalNodes AS Source
        LEFT JOIN Catalog.CanonicalTaxonomyNodes AS Parent
            ON Parent.Code = Source.ParentCode
        WHERE Source.ParentCode IS NOT NULL
          AND Parent.CanonicalTaxonomyNodeId IS NULL
    )
        THROW 51001, 'A CanonicalTaxonomyNode seed references an unknown parent.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @CanonicalNodes AS Source
        LEFT JOIN Catalog.SegmentDefinitions AS Definition
            ON Definition.Code = Source.SegmentDefinitionCode
        WHERE Source.SegmentDefinitionCode IS NOT NULL
          AND Definition.SegmentDefinitionId IS NULL
    )
        THROW 51002, 'A CanonicalTaxonomyNode seed references an unknown SegmentDefinition.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

/* ====================================================================
   Verification queries
   ==================================================================== */

SELECT
    Node.CanonicalTaxonomyNodeId,
    Node.ParentId,
    Node.Code,
    Node.Name,
    Node.Depth,
    Node.Path,
    Definition.Code AS DirectSegmentCode,
    Definition.Name AS DirectSegmentName,
    Node.Status
FROM Catalog.CanonicalTaxonomyNodes AS Node
LEFT JOIN Catalog.SegmentDefinitions AS Definition
    ON Definition.SegmentDefinitionId = Node.SegmentDefinitionId
ORDER BY Node.Path;

SELECT
    Definition.Code AS SegmentCode,
    Definition.Name AS SegmentName,
    Definition.SelectionMode,
    Definition.IsRequired,
    OptionValue.Code AS OptionCode,
    OptionValue.Name AS OptionName,
    OptionValue.SemanticText,
    OptionValue.DisplayOrder
FROM Catalog.SegmentDefinitions AS Definition
INNER JOIN Catalog.SegmentOptions AS OptionValue
    ON OptionValue.SegmentDefinitionId = Definition.SegmentDefinitionId
ORDER BY Definition.Code, OptionValue.DisplayOrder;

/*
    Resolves every effective segmentation for a node, including definitions
    inherited from all ancestors. Change @RequestedNodeCode as needed.
*/
DECLARE @RequestedNodeCode NVARCHAR(120) = N'running_shoes';

;WITH NodeAncestry AS
(
    SELECT
        Node.CanonicalTaxonomyNodeId,
        Node.ParentId,
        Node.Code,
        Node.Name,
        Node.Depth,
        Node.SegmentDefinitionId,
        0 AS DistanceFromRequestedNode
    FROM Catalog.CanonicalTaxonomyNodes AS Node
    WHERE Node.Code = @RequestedNodeCode

    UNION ALL

    SELECT
        Parent.CanonicalTaxonomyNodeId,
        Parent.ParentId,
        Parent.Code,
        Parent.Name,
        Parent.Depth,
        Parent.SegmentDefinitionId,
        Child.DistanceFromRequestedNode + 1
    FROM Catalog.CanonicalTaxonomyNodes AS Parent
    INNER JOIN NodeAncestry AS Child
        ON Child.ParentId = Parent.CanonicalTaxonomyNodeId
)
SELECT
    Ancestry.Code AS DeclaredByNodeCode,
    Ancestry.Name AS DeclaredByNodeName,
    Definition.Code AS SegmentCode,
    Definition.Name AS SegmentName,
    Definition.SelectionMode,
    Definition.IsRequired,
    OptionValue.Code AS OptionCode,
    OptionValue.Name AS OptionName,
    OptionValue.SemanticText,
    Ancestry.DistanceFromRequestedNode
FROM NodeAncestry AS Ancestry
INNER JOIN Catalog.SegmentDefinitions AS Definition
    ON Definition.SegmentDefinitionId = Ancestry.SegmentDefinitionId
INNER JOIN Catalog.SegmentOptions AS OptionValue
    ON OptionValue.SegmentDefinitionId = Definition.SegmentDefinitionId
   AND OptionValue.Status = N'Active'
WHERE Definition.Status = N'Active'
ORDER BY
    Ancestry.Depth,
    Definition.Code,
    OptionValue.DisplayOrder
OPTION (MAXRECURSION 100);
