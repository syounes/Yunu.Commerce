using Xunit;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

/// <summary>
/// Unit tests for AttributeSemanticDocumentBuilder (docs task: "SKU attribute
/// embedding synchronization pipeline").
/// </summary>
public sealed class AttributeSemanticDocumentBuilderTests
{
    private static AttributeDefinitionSource CreateColorDefinition() => new()
    {
        AttributeDefinitionId = 14,
        Code = "color",
        GoogleAttributeName = "color",
        Name = "Cor",
        Description = "Cor principal ou combinação de cores do SKU.",
        SemanticText = "cor, tonalidade, color, preto, branco, azul, vermelho, variante SKU",
        DataType = "Text",
        Cardinality = "Single",
        UnitFamily = null,
        IsGoogleMerchantAttribute = true,
        IsVariantAxis = true,
        IsSearchable = true,
        IsFilterable = true,
        IsRequiredByDefault = false,
        DisplayOrder = 10,
        IsActive = true,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static AttributeOptionSource CreateGenderMaleOption() => new()
    {
        AttributeOptionId = 1401,
        AttributeDefinitionId = 47,
        AttributeCode = "gender",
        AttributeName = "Gênero",
        OptionCode = "MALE",
        GoogleValue = "male",
        OptionName = "Masculino",
        OptionSemanticText = "produto para homem masculino",
        DisplayOrder = 10,
        IsActive = true
    };

    [Fact]
    public void BuildDefinitionText_Should_Be_Deterministic_For_Same_Input()
    {
        var definition = CreateColorDefinition();

        var first = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);
        var second = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);

        Assert.Equal(first, second);
        Assert.Contains("Atributo: Cor.", first);
        Assert.Contains("Código: color.", first);
        Assert.Contains("Nome Google: color.", first);
        Assert.Contains("Tipo de dado: Text.", first);
        Assert.Contains("Cardinalidade: Single.", first);
        Assert.Contains("Família de unidade: não aplicável.", first);
        Assert.Contains("Eixo de variante: sim.", first);
        Assert.Contains("Pesquisável: sim.", first);
        Assert.Contains("Filtrável: sim.", first);
        Assert.Contains("Obrigatório por padrão: não.", first);
        Assert.DoesNotContain("14", first);
    }

    [Fact]
    public void BuildOptionText_Should_Be_Deterministic_For_Same_Input()
    {
        var option = CreateGenderMaleOption();

        var first = AttributeSemanticDocumentBuilder.BuildOptionText(option);
        var second = AttributeSemanticDocumentBuilder.BuildOptionText(option);

        Assert.Equal(first, second);
        Assert.Contains("Atributo: Gênero.", first);
        Assert.Contains("Código do atributo: gender.", first);
        Assert.Contains("Opção: Masculino.", first);
        Assert.Contains("Código da opção: MALE.", first);
        Assert.Contains("Valor Google Merchant: male.", first);
        Assert.DoesNotContain("1401", first);
        Assert.DoesNotContain("47", first);
    }

    [Fact]
    public void BuildDefinitionText_Should_Omit_Null_Optional_Fields_Without_Meaningless_Labels()
    {
        var definition = CreateColorDefinition();
        definition = new AttributeDefinitionSource
        {
            AttributeDefinitionId = definition.AttributeDefinitionId,
            Code = definition.Code,
            GoogleAttributeName = null,
            Name = definition.Name,
            Description = "",
            SemanticText = "",
            DataType = definition.DataType,
            Cardinality = definition.Cardinality,
            UnitFamily = null,
            IsGoogleMerchantAttribute = definition.IsGoogleMerchantAttribute,
            IsVariantAxis = definition.IsVariantAxis,
            IsSearchable = definition.IsSearchable,
            IsFilterable = definition.IsFilterable,
            IsRequiredByDefault = definition.IsRequiredByDefault,
            DisplayOrder = definition.DisplayOrder,
            IsActive = definition.IsActive,
            UpdatedAt = definition.UpdatedAt
        };

        var text = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);

        Assert.DoesNotContain("Nome Google:", text);
        Assert.DoesNotContain("Descrição:", text);
        Assert.DoesNotContain("Significado semântico:", text);
        Assert.Contains("Família de unidade: não aplicável.", text);
    }

    [Fact]
    public void BuildOptionText_Should_Omit_Null_GoogleValue_Without_Meaningless_Label()
    {
        var option = CreateGenderMaleOption();
        option = new AttributeOptionSource
        {
            AttributeOptionId = option.AttributeOptionId,
            AttributeDefinitionId = option.AttributeDefinitionId,
            AttributeCode = option.AttributeCode,
            AttributeName = option.AttributeName,
            OptionCode = option.OptionCode,
            GoogleValue = null,
            OptionName = option.OptionName,
            OptionSemanticText = "",
            DisplayOrder = option.DisplayOrder,
            IsActive = option.IsActive
        };

        var text = AttributeSemanticDocumentBuilder.BuildOptionText(option);

        Assert.DoesNotContain("Valor Google Merchant:", text);
        Assert.DoesNotContain("Significado semântico:", text);
    }

    [Fact]
    public void ComputeContentHash_Should_Be_Stable_For_Same_Utf8_Text()
    {
        const string text = "Atributo: Cor. Código: color.";

        var first = AttributeSemanticDocumentBuilder.ComputeContentHash(text);
        var second = AttributeSemanticDocumentBuilder.ComputeContentHash(text);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Equal(first, first.ToLowerInvariant());
    }

    [Fact]
    public void ComputeContentHash_Should_Change_When_Text_Changes()
    {
        var hashA = AttributeSemanticDocumentBuilder.ComputeContentHash("Atributo: Cor.");
        var hashB = AttributeSemanticDocumentBuilder.ComputeContentHash("Atributo: Tamanho.");

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void BuildDefinitionEntityId_Should_Use_Attribute_Code()
    {
        Assert.Equal("color", AttributeSemanticDocumentBuilder.BuildDefinitionEntityId("color"));
    }

    [Fact]
    public void BuildOptionEntityId_Should_Use_Composite_Attribute_And_Option_Code()
    {
        Assert.Equal("gender:MALE", AttributeSemanticDocumentBuilder.BuildOptionEntityId("gender", "MALE"));
    }

    /// <summary>
    /// Regression coverage for the "shipping_weight" case (docs task: "SKU
    /// attribute embedding synchronization pipeline"): a non-searchable but
    /// active definition must go through the exact same generic builder as
    /// any other definition, with no attribute-specific hardcoding. Only the
    /// values are equivalent to the real shipping_weight row; the definition
    /// is not identified by a fixed numeric id.
    /// </summary>
    [Fact]
    public void BuildDefinitionText_Should_Produce_Full_Text_For_NonSearchable_Definition()
    {
        var definition = new AttributeDefinitionSource
        {
            AttributeDefinitionId = 999,
            Code = "shipping_weight",
            GoogleAttributeName = "shipping_weight",
            Name = "Peso para frete",
            Description = "Peso usado no cálculo de entrega.",
            SemanticText = "peso embalagem frete transporte cálculo entrega",
            DataType = "Measurement",
            Cardinality = "Single",
            UnitFamily = "Weight",
            IsGoogleMerchantAttribute = true,
            IsVariantAxis = false,
            IsSearchable = false,
            IsFilterable = false,
            IsRequiredByDefault = false,
            DisplayOrder = 40,
            IsActive = true,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var text = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);

        Assert.Contains("Atributo: Peso para frete.", text);
        Assert.Contains("Código: shipping_weight.", text);
        Assert.Contains("Nome Google: shipping_weight.", text);
        Assert.Contains("Descrição: Peso usado no cálculo de entrega.", text);
        Assert.Contains("Significado semântico: peso embalagem frete transporte cálculo entrega.", text);
        Assert.Contains("Tipo de dado: Measurement.", text);
        Assert.Contains("Cardinalidade: Single.", text);
        Assert.Contains("Família de unidade: Weight.", text);
        Assert.Contains("Pesquisável: não.", text);
        Assert.Contains("Filtrável: não.", text);
    }
}
