using Xunit;
using Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure.Tests.IntentRewriting;

/// <summary>
/// Contract tests for <see cref="IntentRewriterSystemPrompt"/> (docs task:
/// "Google Category semantic resolution — categorySearchQuery extraction
/// regression"). These tests do not compare the whole prompt text (fragile);
/// they assert the presence of specific, load-bearing instructions so the
/// prompt cannot silently regress into telling the model to drop attributes
/// globally instead of only from categorySearchQuery.
/// </summary>
public sealed class IntentRewriterSystemPromptTests
{
    /// <summary>
    /// The raw string literal wraps prose across lines for readability; tests
    /// that need to assert a phrase spanning a line break use this
    /// whitespace-normalized (single-spaced) view so they don't depend on the
    /// exact column where the prompt happens to wrap.
    /// </summary>
    private static readonly string NormalizedText =
        System.Text.RegularExpressions.Regex.Replace(IntentRewriterSystemPrompt.Text, "\\s+", " ");

    [Fact]
    public void Prompt_targets_ptBR()
    {
        Assert.Contains("português", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pt-BR", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_official_category_ids()
    {
        Assert.Contains("Nunca escolha ou produza identificadores oficiais", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_disambiguation_rule_for_ambiguous_terms()
    {
        Assert.Contains("desambiguar", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sapatos esportivos para corrida", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_scopes_removal_rules_exclusively_to_categorySearchQuery()
    {
        Assert.Contains("aplica-se EXCLUSIVAMENTE", IntentRewriterSystemPrompt.Text);
        Assert.Contains("categorySearchQuery", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_attributeHints_to_capture_all_explicit_facts()
    {
        Assert.Contains("TODOS os fatos explícitos", IntentRewriterSystemPrompt.Text);
        Assert.Contains("condição", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("peso para entrega", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_forbids_omitting_logistics_facts_from_attributeHints()
    {
        Assert.Contains("dado logístico", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Nunca omita um fato de attributeHints apenas porque ele",
            IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_states_same_fact_can_appear_in_both_categorySearchQuery_and_attributeHints()
    {
        Assert.Contains("também foi usado como qualificador", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_semanticQuery_to_preserve_condition_and_shipping_weight()
    {
        Assert.Contains(
            "semanticQuery NUNCA deve perder fatos relevantes",
            IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_inventing_absent_facts()
    {
        Assert.Contains("Nunca invente um fato que o usuário não mencionou", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_full_running_shoes_regression_example()
    {
        Assert.Contains(
            "produto novo e com peso para entrega de 2 kg",
            IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"finalidade de uso\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"condição\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"peso para entrega\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawValue\": \"2 kg\"", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_decomposing_compound_attributes_into_atomic_hints()
    {
        Assert.Contains("attributeHint SEPARADO para CADA atributo atômico", IntentRewriterSystemPrompt.Text);
        Assert.Contains("produza um único hint chamado \"dimensões da embalagem\"", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_semanticQuery_to_preserve_full_product_context_never_simplified()
    {
        Assert.Contains("apenas categorySearchQuery deve ser simplificada", IntentRewriterSystemPrompt.Text);
        Assert.Contains("NUNCA deve ser simplificada dessa forma", NormalizedText);
    }

    [Fact]
    public void Prompt_contains_full_microphone_compound_dimensions_regression_example()
    {
        Assert.Contains("microfone condensador USB preto", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"comprimento da embalagem\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"largura da embalagem\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"altura da embalagem\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"tipo de conexão\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("categorySearchQuery: \"microfones\"", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_extracting_technical_qualifiers_even_when_present_in_other_fields()
    {
        Assert.Contains("Extraia também qualificadores técnicos explícitos do produto como", IntentRewriterSystemPrompt.Text);
        Assert.Contains("attributeHint próprio com rawName", NormalizedText);
        Assert.Contains("contextual e específico (ex.: { \"rawName\": \"tipo de microfone\", \"rawValue\": \"condensador\" }), além de continuar aparecendo em categoryHint", NormalizedText);
        Assert.Contains("deduplique um fato removendo-o de attributeHints", NormalizedText);
    }

    [Fact]
    public void Prompt_instructs_contextual_specific_rawNames_and_forbids_bare_tipo()
    {
        Assert.Contains("nunca uma palavra vaga", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"tipo\" (prefira \"tipo de teclado\", \"tipo de microfone\", \"tipo de conexão\"", NormalizedText);
        Assert.Contains("nunca a implemente mentalmente como uma", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_artificial_booleans_for_mode_expressions()
    {
        Assert.Contains("Não converta expressões que já carregam o valor semântico da opção em", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"modo de conexão\", \"rawValue\": \"com fio\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("nunca { \"rawName\": \"com fio\", \"rawValue\": \"sim\" }", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_category_identity_rule_excluding_para_computador_as_attribute()
    {
        Assert.Contains("REGRA DE IDENTIDADE DE CATEGORIA", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"para computador\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("então NÃO deve gerar um attributeHint como { \"rawName\": \"uso\", \"rawValue\": \"para computador\" }", NormalizedText);
    }

    [Fact]
    public void Prompt_contains_operational_instruction_exclusion_rule_and_single_sku_example()
    {
        Assert.Contains("REGRA DE EXCLUSÃO DE INSTRUÇÕES OPERACIONAIS", IntentRewriterSystemPrompt.Text);
        Assert.Contains("attributeHints: \"deve possuir um único SKU\"", NormalizedText);
        Assert.Contains("NUNCA deve gerar um attributeHint", NormalizedText);
    }

    [Fact]
    public void Prompt_distinguishes_commercial_quantity_from_sku_creation_instruction()
    {
        Assert.Contains("pacote com 3 camisetas", IntentRewriterSystemPrompt.Text);
        Assert.Contains("quantidade por embalagem", NormalizedText);
        Assert.Contains("criar um único SKU\" para esse pacote", NormalizedText);
        Assert.Contains("instrução sobre SKU continua sendo ignorada", NormalizedText);
    }

    [Fact]
    public void Prompt_contains_full_keyboard_regression_example_with_expected_hints()
    {
        Assert.Contains("teclado mecânico para computador", IntentRewriterSystemPrompt.Text);
        Assert.Contains("categorySearchQuery: \"teclados mecânicos\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"tipo de teclado\", \"rawValue\": \"mecânico\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"modo de conexão\", \"rawValue\": \"com fio\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"peso para entrega\", \"rawValue\": \"1,2 kg\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"comprimento da embalagem\", \"rawValue\": \"45 cm\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"largura da embalagem\", \"rawValue\": \"18 cm\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"altura da embalagem\", \"rawValue\": \"6 cm\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains(
            "NÃO produza: \"tipo\" = \"mecânico\"; \"uso\" ou \"ocasião\" = \"para computador\"; \"com fio\" = \"sim\"; \"SKU\" = \"único\"; \"title\" = \"único\".",
            NormalizedText);
        Assert.Contains("attributeHint, nem como \"SKU\", nem como \"title\", nem como", NormalizedText);
    }

    [Fact]
    public void Prompt_contains_tshirt_and_microphone_scenarios_with_contextual_rawNames()
    {
        Assert.Contains("camiseta masculina preta, tamanho M, de algodão e produto novo", NormalizedText);
        Assert.Contains("- { \"rawName\": \"gênero\", \"rawValue\": \"masculino\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("- { \"rawName\": \"tipo de microfone\", \"rawValue\": \"condensador\" }", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_explicit_compatibility_example()
    {
        Assert.Contains("capa compatível com iPhone 15", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"compatibilidade\", \"rawValue\": \"iPhone 15\" }", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_is_versioned_v6()
    {
        Assert.Equal("v6", IntentRewriterSystemPrompt.Version);
    }

    [Fact]
    public void Prompt_instructs_one_property_per_attributeHint_with_decomposition_examples()
    {
        Assert.Contains(
            "Cada attributeHint deve representar exatamente uma propriedade independente",
            NormalizedText);
        Assert.Contains("tamanho 38 no sistema brasileiro", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"sistema de tamanho\", \"rawValue\": \"brasileiro\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("SSD de 1 TB", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"tipo de armazenamento\", \"rawValue\": \"SSD\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"armazenamento\", \"rawValue\": \"1 TB\" }", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_splitting_number_from_unit_and_atomic_semantic_values()
    {
        Assert.Contains(
            "NÃO decomponha um número e sua unidade de medida",
            NormalizedText);
        Assert.Contains("620 g", IntentRewriterSystemPrompt.Text);
        Assert.Contains("USB-C", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Wi-Fi", IntentRewriterSystemPrompt.Text);
    }
}
