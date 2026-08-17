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
    public void Prompt_forbids_official_ids()
    {
        Assert.Contains("Nunca produza IDs oficiais de categoria, atributo, opção, produto ou SKU", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_inventing_information()
    {
        Assert.Contains("Nunca invente informações", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_disambiguation_rule_for_ambiguous_terms()
    {
        Assert.Contains("Desambigue palavras com mais de um significado", IntentRewriterSystemPrompt.Text);
        Assert.Contains("sapatos esportivos", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_scopes_removal_rules_exclusively_to_categorySearchQuery()
    {
        Assert.Contains("Remova exclusivamente de categorySearchQuery", IntentRewriterSystemPrompt.Text);
        Assert.Contains(
            "Essa remoção vale somente para categorySearchQuery. Os mesmos fatos devem",
            IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_attributeHints_to_capture_all_explicit_facts()
    {
        Assert.Contains("Extraia todos os fatos comerciais explicitamente informados", IntentRewriterSystemPrompt.Text);
        Assert.Contains("condição", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("peso para frete", IntentRewriterSystemPrompt.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_forbids_vague_rawNames()
    {
        Assert.Contains("Evite nomes vagos como", IntentRewriterSystemPrompt.Text);
        Assert.Contains("tipo de teclado", IntentRewriterSystemPrompt.Text);
        Assert.Contains("tipo de microfone", IntentRewriterSystemPrompt.Text);
        Assert.Contains("tipo de conexão", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_fact_decision_order_rule()
    {
        Assert.Contains("# 9. Ordem de decisão dos fatos", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Nunca descarte uma propriedade apenas porque ela também aparece em", NormalizedText);
    }

    [Fact]
    public void Prompt_contains_occasion_extraction_rule_with_canonical_rawName()
    {
        Assert.Contains("# 10. Ocasião e finalidade", IntentRewriterSystemPrompt.Text);
        Assert.Contains("ocasião = corrida", IntentRewriterSystemPrompt.Text);
        Assert.Contains("ocasião = festa", IntentRewriterSystemPrompt.Text);
        Assert.Contains("ocasião = academia", IntentRewriterSystemPrompt.Text);
        Assert.Contains(
            "Não omita ocasião apenas porque a finalidade foi usada na identificação",
            NormalizedText);
    }

    [Fact]
    public void Prompt_contains_compatibility_rule_and_distinguishes_from_occasion()
    {
        Assert.Contains("# 11. Compatibilidade", IntentRewriterSystemPrompt.Text);
        Assert.Contains("compatibilidade = iPhone 15", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Não trate compatibilidade como ocasião", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_instructs_decomposing_compound_attributes_into_atomic_hints()
    {
        Assert.Contains("# 13. Atributos compostos", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Separe propriedades diferentes que aparecem na mesma expressão", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"comprimento da embalagem\", \"rawValue\": \"34 cm\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"largura da embalagem\", \"rawValue\": \"22 cm\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"altura da embalagem\", \"rawValue\": \"12 cm\"", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_splitting_number_and_unit()
    {
        Assert.Contains("Não separe número e unidade", IntentRewriterSystemPrompt.Text);
        Assert.Contains("620 g permanece um único valor", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_fragmenting_indivisible_values()
    {
        Assert.Contains("Não fragmente valores semanticamente indivisíveis", IntentRewriterSystemPrompt.Text);
        Assert.Contains("USB-C", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Wi-Fi", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_forbids_artificial_booleans_for_mode_expressions()
    {
        Assert.Contains("Não transforme valores semanticamente importantes em booleanos", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"modo de conexão\", \"rawValue\": \"com fio\" }", IntentRewriterSystemPrompt.Text);
        Assert.Contains("{ \"rawName\": \"com fio\", \"rawValue\": \"sim\" }", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_separates_product_and_logistics_weight_dimensions()
    {
        Assert.Contains("# 14. Pesos e dimensões", IntentRewriterSystemPrompt.Text);
        Assert.Contains("peso do produto = 620 g", IntentRewriterSystemPrompt.Text);
        Assert.Contains("peso para frete = 850 g", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_operational_instruction_exclusion_section()
    {
        Assert.Contains("# 3. Instruções operacionais", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Não transforme quantidade solicitada de SKUs em atributo", IntentRewriterSystemPrompt.Text);
        Assert.Contains("crie um único SKU", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_distinguishes_commercial_quantity_from_sku_creation_instruction()
    {
        Assert.Contains("pacote com 3 unidades", IntentRewriterSystemPrompt.Text);
        Assert.Contains("fato comercial, extrair quantidade por embalagem", NormalizedText);
    }

    [Fact]
    public void Prompt_contains_full_running_shoes_regression_example()
    {
        Assert.Contains("# 15. Exemplo completo obrigatório", IntentRewriterSystemPrompt.Text);
        Assert.Contains("tênis feminino para corrida", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"ocasião\", \"rawValue\": \"corrida\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"condição\", \"rawValue\": \"novo\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"peso do produto\", \"rawValue\": \"620 g\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"peso para frete\", \"rawValue\": \"850 g\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains(
            "A instrução \"somente uma variação de SKU\" deve ser ignorada porque não",
            IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_contains_final_verification_checklist()
    {
        Assert.Contains("# 16. Verificação antes da resposta", IntentRewriterSystemPrompt.Text);
        Assert.Contains("nenhuma informação foi inventada", IntentRewriterSystemPrompt.Text);
        Assert.Contains("nenhum ID oficial foi produzido", IntentRewriterSystemPrompt.Text);
        Assert.Contains("Retorne somente o JSON compatível com o schema fornecido", IntentRewriterSystemPrompt.Text);
    }

    [Fact]
    public void Prompt_is_versioned_v8()
    {
        Assert.Equal("v8", IntentRewriterSystemPrompt.Version);
    }
}
