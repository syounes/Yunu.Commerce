/*
    Yunu.Commerce - Add cable_length attribute

    Características:
      - SQL Server
      - Idempotente
      - Reutiliza as configurações de Measurement de product_length
      - Suporta AttributeDefinitionId com ou sem IDENTITY
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    DECLARE @ProductLengthDefinitionId int;
    DECLARE @CableLengthDefinitionId int;
    DECLARE @NextDisplayOrder smallint;

    DECLARE @IsDefinitionIdentity bit = CONVERT(
        bit,
        COLUMNPROPERTY(
            OBJECT_ID(N'Catalog.AttributeDefinitions'),
            N'AttributeDefinitionId',
            'IsIdentity'));

    SELECT
        @ProductLengthDefinitionId = d.AttributeDefinitionId
    FROM Catalog.AttributeDefinitions AS d WITH (UPDLOCK, HOLDLOCK)
    WHERE d.Code = 'product_length';

    IF @ProductLengthDefinitionId IS NULL
    BEGIN
        THROW 50001,
            'Required reference definition product_length was not found in Catalog.AttributeDefinitions.',
            1;
    END;

    SELECT
        @CableLengthDefinitionId = d.AttributeDefinitionId
    FROM Catalog.AttributeDefinitions AS d WITH (UPDLOCK, HOLDLOCK)
    WHERE d.Code = 'cable_length';

    /*
        Atualiza caso cable_length já exista.
        As configurações de Measurement são herdadas de product_length.
    */
    IF @CableLengthDefinitionId IS NOT NULL
    BEGIN
        UPDATE target
        SET
            AttributeGroupId = source.AttributeGroupId,
            GoogleAttributeName = NULL,
            Name = N'Comprimento do cabo',
            Description =
                N'Extensão física do cabo pertencente ou fornecido com o produto. '
                + N'Não representa o comprimento do produto nem da embalagem.',
            SemanticText =
                N'comprimento do cabo tamanho do cabo extensão do cabo medida do cabo '
                + N'comprimento do fio cabo em metros cabo em centímetros '
                + N'cable length cord length wire length',
            DataType = source.DataType,
            Cardinality = source.Cardinality,
            UnitFamily = source.UnitFamily,
            ValidationRegex = source.ValidationRegex,
            MinNumericValue = source.MinNumericValue,
            MaxNumericValue = source.MaxNumericValue,
            MaxLength = source.MaxLength,
            IsGoogleMerchantAttribute = 0,
            IsVariantAxis = 0,
            IsSearchable = 1,
            IsFilterable = 1,
            IsRequiredByDefault = 0,
            IsActive = 1,
            UpdatedAt = @Now
        FROM Catalog.AttributeDefinitions AS target
        CROSS JOIN Catalog.AttributeDefinitions AS source
        WHERE target.AttributeDefinitionId = @CableLengthDefinitionId
          AND source.AttributeDefinitionId = @ProductLengthDefinitionId;
    END
    ELSE
    BEGIN
        SET @NextDisplayOrder = CONVERT(
            smallint,
            ISNULL((
                SELECT MAX(d.DisplayOrder)
                FROM Catalog.AttributeDefinitions AS d
                    WITH (UPDLOCK, HOLDLOCK)
            ), 0) + 1);

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
            SELECT
                source.AttributeGroupId,
                'cable_length',
                NULL,
                N'Comprimento do cabo',
                N'Extensão física do cabo pertencente ou fornecido com o produto. '
                    + N'Não representa o comprimento do produto nem da embalagem.',
                N'comprimento do cabo tamanho do cabo extensão do cabo medida do cabo '
                    + N'comprimento do fio cabo em metros cabo em centímetros '
                    + N'cable length cord length wire length',
                source.DataType,
                source.Cardinality,
                source.UnitFamily,
                source.ValidationRegex,
                source.MinNumericValue,
                source.MaxNumericValue,
                source.MaxLength,
                0,
                0,
                1,
                1,
                0,
                @NextDisplayOrder,
                1,
                @Now,
                @Now
            FROM Catalog.AttributeDefinitions AS source
            WHERE source.AttributeDefinitionId = @ProductLengthDefinitionId;
        END
        ELSE
        BEGIN
            SET @CableLengthDefinitionId =
                ISNULL((
                    SELECT MAX(d.AttributeDefinitionId)
                    FROM Catalog.AttributeDefinitions AS d
                        WITH (UPDLOCK, HOLDLOCK)
                ), 0) + 1;

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
            SELECT
                @CableLengthDefinitionId,
                source.AttributeGroupId,
                'cable_length',
                NULL,
                N'Comprimento do cabo',
                N'Extensão física do cabo pertencente ou fornecido com o produto. '
                    + N'Não representa o comprimento do produto nem da embalagem.',
                N'comprimento do cabo tamanho do cabo extensão do cabo medida do cabo '
                    + N'comprimento do fio cabo em metros cabo em centímetros '
                    + N'cable length cord length wire length',
                source.DataType,
                source.Cardinality,
                source.UnitFamily,
                source.ValidationRegex,
                source.MinNumericValue,
                source.MaxNumericValue,
                source.MaxLength,
                0,
                0,
                1,
                1,
                0,
                @NextDisplayOrder,
                1,
                @Now,
                @Now
            FROM Catalog.AttributeDefinitions AS source
            WHERE source.AttributeDefinitionId = @ProductLengthDefinitionId;
        END;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT
    AttributeDefinitionId,
    AttributeGroupId,
    Code,
    Name,
    Description,
    SemanticText,
    DataType,
    Cardinality,
    UnitFamily,
    IsSearchable,
    IsFilterable,
    IsActive,
    CreatedAt,
    UpdatedAt
FROM Catalog.AttributeDefinitions
WHERE Code = 'cable_length';