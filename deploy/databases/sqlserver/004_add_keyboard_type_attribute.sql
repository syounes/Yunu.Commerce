/*
    Yunu.Commerce - Catalog attribute refinement

    Changes:
      1. Narrows connection_type to concrete interfaces/connectors/protocols.
      2. Adds connectivity_mode and its options.
      3. Adds keyboard_type and its options.

    Characteristics:
      - SQL Server
      - Idempotent (safe to execute more than once)
      - Preserves existing valid connection_type options
      - Supports both IDENTITY and non-IDENTITY integer primary keys
      - Uses the same AttributeGroupId as connection_type
      - Uses the existing single-value Cardinality convention from condition
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    DECLARE @AttributeGroupId smallint;
    DECLARE @SingleCardinality varchar(20);
    DECLARE @ConnectionTypeDefinitionId int;
    DECLARE @IsDefinitionIdentity bit = CONVERT(
        bit,
        COLUMNPROPERTY(
            OBJECT_ID(N'Catalog.AttributeDefinitions'),
            N'AttributeDefinitionId',
            'IsIdentity'));
    DECLARE @IsOptionIdentity bit = CONVERT(
        bit,
        COLUMNPROPERTY(
            OBJECT_ID(N'Catalog.AttributeOptions'),
            N'AttributeOptionId',
            'IsIdentity'));

    SELECT
        @ConnectionTypeDefinitionId = d.AttributeDefinitionId,
        @AttributeGroupId = d.AttributeGroupId
    FROM Catalog.AttributeDefinitions AS d WITH (UPDLOCK, HOLDLOCK)
    WHERE d.Code = 'connection_type';

    IF @ConnectionTypeDefinitionId IS NULL
    BEGIN
        THROW 50001,
            'Required definition connection_type was not found in Catalog.AttributeDefinitions.',
            1;
    END;

    /* Reuse the exact single-value convention already used by the catalog. */
    SELECT @SingleCardinality = d.Cardinality
    FROM Catalog.AttributeDefinitions AS d
    WHERE d.Code = 'condition';

    IF @SingleCardinality IS NULL
    BEGIN
        THROW 50002,
            'Required reference definition condition was not found; unable to determine the catalog single-value Cardinality.',
            1;
    END;

    /*
        connection_type represents a concrete interface/connector/technology.
        Generic wired/wireless terms belong to connectivity_mode.
    */
    UPDATE Catalog.AttributeDefinitions
    SET
        Description = N'Interface, conector ou tecnologia específica de comunicação suportada pelo item. Aceita múltiplos valores simultâneos.',
        SemanticText = N'tipo de conexão interface conector entrada saída porta protocolo tecnologia usb usb-c bluetooth wi-fi p2 p3 p10 xlr hdmi ethernet rj45',
        UpdatedAt = @Now
    WHERE AttributeDefinitionId = @ConnectionTypeDefinitionId;

    DECLARE @NextDefinitionDisplayOrder smallint =
        CONVERT(smallint, ISNULL((
            SELECT MAX(d.DisplayOrder)
            FROM Catalog.AttributeDefinitions AS d WITH (UPDLOCK, HOLDLOCK)), 0) + 1);

    /* Upsert connectivity_mode. */
    UPDATE Catalog.AttributeDefinitions
    SET
        AttributeGroupId = @AttributeGroupId,
        GoogleAttributeName = NULL,
        Name = N'Modo de conexão',
        Description = N'Indica se o produto se conecta por cabo, sem fio ou suporta os dois modos.',
        SemanticText = N'modo de conexão com fio cabeado sem fio híbrido wired wireless conexão cabeada conexão sem fio',
        DataType = 'Enum',
        Cardinality = @SingleCardinality,
        UnitFamily = NULL,
        ValidationRegex = NULL,
        MinNumericValue = NULL,
        MaxNumericValue = NULL,
        MaxLength = NULL,
        IsGoogleMerchantAttribute = 0,
        IsVariantAxis = 0,
        IsSearchable = 1,
        IsFilterable = 1,
        IsRequiredByDefault = 0,
        IsActive = 1,
        UpdatedAt = @Now
    WHERE Code = 'connectivity_mode';

    IF @@ROWCOUNT = 0
    BEGIN
        IF @IsDefinitionIdentity = 1
        BEGIN
            INSERT INTO Catalog.AttributeDefinitions
            (
                AttributeGroupId,
                Code,
                GoogleAttributeName,
                Name,
                Description,
                SemanticText,
                DataType,
                Cardinality,
                UnitFamily,
                ValidationRegex,
                MinNumericValue,
                MaxNumericValue,
                MaxLength,
                IsGoogleMerchantAttribute,
                IsVariantAxis,
                IsSearchable,
                IsFilterable,
                IsRequiredByDefault,
                DisplayOrder,
                IsActive,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                @AttributeGroupId,
                'connectivity_mode',
                NULL,
                N'Modo de conexão',
                N'Indica se o produto se conecta por cabo, sem fio ou suporta os dois modos.',
                N'modo de conexão com fio cabeado sem fio híbrido wired wireless conexão cabeada conexão sem fio',
                'Enum',
                @SingleCardinality,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0,
                0,
                1,
                1,
                0,
                @NextDefinitionDisplayOrder,
                1,
                @Now,
                @Now
            );
        END
        ELSE
        BEGIN
            DECLARE @ConnectivityModeDefinitionId int =
                ISNULL((
                    SELECT MAX(d.AttributeDefinitionId)
                    FROM Catalog.AttributeDefinitions AS d WITH (UPDLOCK, HOLDLOCK)), 0) + 1;

            INSERT INTO Catalog.AttributeDefinitions
            (
                AttributeDefinitionId,
                AttributeGroupId,
                Code,
                GoogleAttributeName,
                Name,
                Description,
                SemanticText,
                DataType,
                Cardinality,
                UnitFamily,
                ValidationRegex,
                MinNumericValue,
                MaxNumericValue,
                MaxLength,
                IsGoogleMerchantAttribute,
                IsVariantAxis,
                IsSearchable,
                IsFilterable,
                IsRequiredByDefault,
                DisplayOrder,
                IsActive,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                @ConnectivityModeDefinitionId,
                @AttributeGroupId,
                'connectivity_mode',
                NULL,
                N'Modo de conexão',
                N'Indica se o produto se conecta por cabo, sem fio ou suporta os dois modos.',
                N'modo de conexão com fio cabeado sem fio híbrido wired wireless conexão cabeada conexão sem fio',
                'Enum',
                @SingleCardinality,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0,
                0,
                1,
                1,
                0,
                @NextDefinitionDisplayOrder,
                1,
                @Now,
                @Now
            );
        END;

        SET @NextDefinitionDisplayOrder += 1;
    END;

    /* Upsert keyboard_type. */
    UPDATE Catalog.AttributeDefinitions
    SET
        AttributeGroupId = @AttributeGroupId,
        GoogleAttributeName = NULL,
        Name = N'Tipo de teclado',
        Description = N'Mecanismo ou tecnologia de acionamento das teclas do teclado.',
        SemanticText = N'tipo de teclado mecanismo acionamento teclas mecânico membrana tesoura óptico mechanical membrane scissor optical switches',
        DataType = 'Enum',
        Cardinality = @SingleCardinality,
        UnitFamily = NULL,
        ValidationRegex = NULL,
        MinNumericValue = NULL,
        MaxNumericValue = NULL,
        MaxLength = NULL,
        IsGoogleMerchantAttribute = 0,
        IsVariantAxis = 0,
        IsSearchable = 1,
        IsFilterable = 1,
        IsRequiredByDefault = 0,
        IsActive = 1,
        UpdatedAt = @Now
    WHERE Code = 'keyboard_type';

    IF @@ROWCOUNT = 0
    BEGIN
        IF @IsDefinitionIdentity = 1
        BEGIN
            INSERT INTO Catalog.AttributeDefinitions
            (
                AttributeGroupId,
                Code,
                GoogleAttributeName,
                Name,
                Description,
                SemanticText,
                DataType,
                Cardinality,
                UnitFamily,
                ValidationRegex,
                MinNumericValue,
                MaxNumericValue,
                MaxLength,
                IsGoogleMerchantAttribute,
                IsVariantAxis,
                IsSearchable,
                IsFilterable,
                IsRequiredByDefault,
                DisplayOrder,
                IsActive,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                @AttributeGroupId,
                'keyboard_type',
                NULL,
                N'Tipo de teclado',
                N'Mecanismo ou tecnologia de acionamento das teclas do teclado.',
                N'tipo de teclado mecanismo acionamento teclas mecânico membrana tesoura óptico mechanical membrane scissor optical switches',
                'Enum',
                @SingleCardinality,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0,
                0,
                1,
                1,
                0,
                @NextDefinitionDisplayOrder,
                1,
                @Now,
                @Now
            );
        END
        ELSE
        BEGIN
            DECLARE @KeyboardTypeDefinitionIdForInsert int =
                ISNULL((
                    SELECT MAX(d.AttributeDefinitionId)
                    FROM Catalog.AttributeDefinitions AS d WITH (UPDLOCK, HOLDLOCK)), 0) + 1;

            INSERT INTO Catalog.AttributeDefinitions
            (
                AttributeDefinitionId,
                AttributeGroupId,
                Code,
                GoogleAttributeName,
                Name,
                Description,
                SemanticText,
                DataType,
                Cardinality,
                UnitFamily,
                ValidationRegex,
                MinNumericValue,
                MaxNumericValue,
                MaxLength,
                IsGoogleMerchantAttribute,
                IsVariantAxis,
                IsSearchable,
                IsFilterable,
                IsRequiredByDefault,
                DisplayOrder,
                IsActive,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                @KeyboardTypeDefinitionIdForInsert,
                @AttributeGroupId,
                'keyboard_type',
                NULL,
                N'Tipo de teclado',
                N'Mecanismo ou tecnologia de acionamento das teclas do teclado.',
                N'tipo de teclado mecanismo acionamento teclas mecânico membrana tesoura óptico mechanical membrane scissor optical switches',
                'Enum',
                @SingleCardinality,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                0,
                0,
                1,
                1,
                0,
                @NextDefinitionDisplayOrder,
                1,
                @Now,
                @Now
            );
        END;
    END;

    DECLARE @DesiredOptions TABLE
    (
        DefinitionCode varchar(100) NOT NULL,
        Code varchar(100) NOT NULL,
        GoogleValue varchar(100) NULL,
        Name nvarchar(150) NOT NULL,
        SemanticText nvarchar(1000) NOT NULL,
        DisplayOrder smallint NOT NULL,
        IsActive bit NOT NULL,
        PRIMARY KEY (DefinitionCode, Code)
    );

    INSERT INTO @DesiredOptions
    (
        DefinitionCode,
        Code,
        GoogleValue,
        Name,
        SemanticText,
        DisplayOrder,
        IsActive
    )
    VALUES
        ('connectivity_mode', 'WIRED', NULL, N'Com fio',
         N'com fio cabeado conexão cabeada cabo wired wired connection', 1, 1),
        ('connectivity_mode', 'WIRELESS', NULL, N'Sem fio',
         N'sem fio wireless conexão sem fio conexão wireless', 2, 1),
        ('connectivity_mode', 'HYBRID', NULL, N'Híbrido',
         N'com fio e sem fio híbrido cabeado e wireless wired and wireless', 3, 1),
        ('keyboard_type', 'MECHANICAL', NULL, N'Mecânico',
         N'teclado mecânico mecânico mechanical keyboard switches mecânicos', 1, 1),
        ('keyboard_type', 'MEMBRANE', NULL, N'Membrana',
         N'teclado de membrana membrana membrane keyboard', 2, 1),
        ('keyboard_type', 'SCISSOR', NULL, N'Tesoura',
         N'teclado tesoura mecanismo tesoura scissor keyboard', 3, 1),
        ('keyboard_type', 'OPTICAL', NULL, N'Óptico',
         N'teclado óptico switch óptico optical keyboard optical switch', 4, 1);

    /* Fail instead of silently reusing an option code owned by another definition. */
    IF EXISTS
    (
        SELECT 1
        FROM @DesiredOptions AS desired
        INNER JOIN Catalog.AttributeOptions AS existing
            ON existing.Code = desired.Code
        INNER JOIN Catalog.AttributeDefinitions AS ownerDefinition
            ON ownerDefinition.AttributeDefinitionId = existing.AttributeDefinitionId
        WHERE ownerDefinition.Code <> desired.DefinitionCode
    )
    BEGIN
        THROW 50003,
            'One of the desired option codes already belongs to another attribute definition.',
            1;
    END;

    /* Update existing options. */
    UPDATE existing
    SET
        existing.GoogleValue = desired.GoogleValue,
        existing.Name = desired.Name,
        existing.SemanticText = desired.SemanticText,
        existing.DisplayOrder = desired.DisplayOrder,
        existing.IsActive = desired.IsActive
    FROM Catalog.AttributeOptions AS existing
    INNER JOIN Catalog.AttributeDefinitions AS definition
        ON definition.AttributeDefinitionId = existing.AttributeDefinitionId
    INNER JOIN @DesiredOptions AS desired
        ON desired.DefinitionCode = definition.Code
       AND desired.Code = existing.Code;

    /* Insert only missing options. */
    IF @IsOptionIdentity = 1
    BEGIN
        INSERT INTO Catalog.AttributeOptions
        (
            AttributeDefinitionId,
            Code,
            GoogleValue,
            Name,
            SemanticText,
            DisplayOrder,
            IsActive
        )
        SELECT
            definition.AttributeDefinitionId,
            desired.Code,
            desired.GoogleValue,
            desired.Name,
            desired.SemanticText,
            desired.DisplayOrder,
            desired.IsActive
        FROM @DesiredOptions AS desired
        INNER JOIN Catalog.AttributeDefinitions AS definition
            ON definition.Code = desired.DefinitionCode
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM Catalog.AttributeOptions AS existing
            WHERE existing.AttributeDefinitionId = definition.AttributeDefinitionId
              AND existing.Code = desired.Code
        );
    END
    ELSE
    BEGIN
        DECLARE @NextOptionId int =
            ISNULL((
                SELECT MAX(o.AttributeOptionId)
                FROM Catalog.AttributeOptions AS o WITH (UPDLOCK, HOLDLOCK)), 0) + 1;

        ;WITH MissingOptions AS
        (
            SELECT
                definition.AttributeDefinitionId,
                desired.Code,
                desired.GoogleValue,
                desired.Name,
                desired.SemanticText,
                desired.DisplayOrder,
                desired.IsActive,
                ROW_NUMBER() OVER
                (
                    ORDER BY desired.DefinitionCode, desired.DisplayOrder, desired.Code
                ) AS RowNumber
            FROM @DesiredOptions AS desired
            INNER JOIN Catalog.AttributeDefinitions AS definition
                ON definition.Code = desired.DefinitionCode
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM Catalog.AttributeOptions AS existing
                WHERE existing.AttributeDefinitionId = definition.AttributeDefinitionId
                  AND existing.Code = desired.Code
            )
        )
        INSERT INTO Catalog.AttributeOptions
        (
            AttributeOptionId,
            AttributeDefinitionId,
            Code,
            GoogleValue,
            Name,
            SemanticText,
            DisplayOrder,
            IsActive
        )
        SELECT
            @NextOptionId + CONVERT(int, missing.RowNumber) - 1,
            missing.AttributeDefinitionId,
            missing.Code,
            missing.GoogleValue,
            missing.Name,
            missing.SemanticText,
            missing.DisplayOrder,
            missing.IsActive
        FROM MissingOptions AS missing;
    END;

    COMMIT TRANSACTION;

    /* Verification output. */
    SELECT
        d.AttributeDefinitionId,
        d.AttributeGroupId,
        d.Code,
        d.Name,
        d.Description,
        d.SemanticText,
        d.DataType,
        d.Cardinality,
        d.IsSearchable,
        d.IsFilterable,
        d.DisplayOrder,
        d.IsActive,
        d.UpdatedAt
    FROM Catalog.AttributeDefinitions AS d
    WHERE d.Code IN ('connection_type', 'connectivity_mode', 'keyboard_type')
    ORDER BY d.DisplayOrder, d.Code;

    SELECT
        d.Code AS AttributeCode,
        o.AttributeOptionId,
        o.Code AS OptionCode,
        o.Name AS OptionName,
        o.SemanticText,
        o.DisplayOrder,
        o.IsActive
    FROM Catalog.AttributeDefinitions AS d
    INNER JOIN Catalog.AttributeOptions AS o
        ON o.AttributeDefinitionId = d.AttributeDefinitionId
    WHERE d.Code IN ('connection_type', 'connectivity_mode', 'keyboard_type')
    ORDER BY d.Code, o.DisplayOrder, o.Code;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;

