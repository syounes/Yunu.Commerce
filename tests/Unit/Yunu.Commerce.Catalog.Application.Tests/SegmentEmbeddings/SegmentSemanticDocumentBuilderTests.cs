using Xunit;
using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentEmbeddings;

/// <summary>
/// Unit tests for SegmentSemanticDocumentBuilder (docs task: "Implementar
/// sincronização de embeddings de segmentos").
/// </summary>
public sealed class SegmentSemanticDocumentBuilderTests
{
    private static SegmentDefinitionSource CreateDefinition() => new()
    {
        SegmentDefinitionId = 14,
        Code = "gender",
        Name = "Gênero",
        Description = "Público-alvo por gênero.",
        SemanticText = "masculino feminino unissex",
        SelectionMode = "Single",
        AssignmentScope = "ProductWithSkuOverride",
        IsRequired = true,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static SegmentOptionSource CreateOption() => new()
    {
        SegmentOptionId = 1401,
        SegmentDefinitionId = 14,
        SegmentCode = "gender",
        SegmentName = "Gênero",
        OptionCode = "MALE",
        OptionName = "Masculino",
        OptionDescription = "Produtos destinados ao público masculino.",
        OptionSemanticText = "homem masculino",
        AssignmentScope = "ProductWithSkuOverride",
        DisplayOrder = 10,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void BuildDefinitionText_Should_Be_Deterministic_For_Same_Input()
    {
        var definition = CreateDefinition();

        var first = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);
        var second = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);

        Assert.Equal(first, second);
        Assert.Contains("Segmento: Gênero.", first);
        Assert.Contains("Código: gender.", first);
        Assert.Contains("Descrição: Público-alvo por gênero.", first);
        Assert.Contains("Significado semântico: masculino feminino unissex.", first);
        Assert.DoesNotContain("14", first);
        Assert.DoesNotContain("ProductWithSkuOverride", first);
        Assert.DoesNotContain("Single", first);
    }

    [Fact]
    public void BuildOptionText_Should_Be_Deterministic_For_Same_Input()
    {
        var option = CreateOption();

        var first = SegmentSemanticDocumentBuilder.BuildOptionText(option);
        var second = SegmentSemanticDocumentBuilder.BuildOptionText(option);

        Assert.Equal(first, second);
        Assert.Contains("Segmento: Gênero.", first);
        Assert.Contains("Código do segmento: gender.", first);
        Assert.Contains("Opção: Masculino.", first);
        Assert.Contains("Código da opção: MALE.", first);
        Assert.Contains("Descrição: Produtos destinados ao público masculino.", first);
        Assert.Contains("Significado semântico: homem masculino.", first);
        Assert.DoesNotContain("1401", first);
        Assert.DoesNotContain("ProductWithSkuOverride", first);
    }

    [Fact]
    public void BuildDefinitionText_Should_Produce_NonEmpty_Text_When_Optional_Fields_Are_Null()
    {
        var definition = new SegmentDefinitionSource
        {
            SegmentDefinitionId = 20,
            Code = "sport_modality",
            Name = "Modalidade esportiva",
            Description = null,
            SemanticText = null,
            SelectionMode = "Multiple",
            AssignmentScope = "Product",
            IsRequired = false,
            UpdatedAt = DateTime.UtcNow
        };

        var text = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("Segmento: Modalidade esportiva.", text);
        Assert.Contains("Código: sport_modality.", text);
        Assert.DoesNotContain("Descrição:", text);
        Assert.DoesNotContain("Significado semântico:", text);
    }

    [Fact]
    public void BuildOptionText_Should_Produce_NonEmpty_Text_When_Optional_Fields_Are_Null()
    {
        var option = new SegmentOptionSource
        {
            SegmentOptionId = 21,
            SegmentDefinitionId = 20,
            SegmentCode = "sport_modality",
            SegmentName = "Modalidade esportiva",
            OptionCode = "RUNNING",
            OptionName = "Corrida",
            OptionDescription = null,
            OptionSemanticText = null,
            AssignmentScope = "Product",
            DisplayOrder = 0,
            UpdatedAt = DateTime.UtcNow
        };

        var text = SegmentSemanticDocumentBuilder.BuildOptionText(option);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("Opção: Corrida.", text);
        Assert.Contains("Código da opção: RUNNING.", text);
        Assert.DoesNotContain("Descrição:", text);
        Assert.DoesNotContain("Significado semântico:", text);
    }

    [Fact]
    public void ComputeContentHash_Should_Match_Known_Sha256_Hex_Value()
    {
        // sha256("abc") lowercase hex, matching
        // encode(digest(convert_to('abc','UTF8'),'sha256'),'hex') in PostgreSQL.
        const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        var hash = SegmentSemanticDocumentBuilder.ComputeContentHash("abc");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeContentHash_Should_Be_Deterministic_And_Sensitive_To_Text_Changes()
    {
        var hash1 = SegmentSemanticDocumentBuilder.ComputeContentHash("Segmento: Gênero.");
        var hash2 = SegmentSemanticDocumentBuilder.ComputeContentHash("Segmento: Gênero.");
        var hash3 = SegmentSemanticDocumentBuilder.ComputeContentHash("Segmento: Genero.");

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
    }
}
