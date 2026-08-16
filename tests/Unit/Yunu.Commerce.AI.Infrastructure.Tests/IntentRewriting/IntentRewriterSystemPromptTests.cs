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
        Assert.Contains("\"rawName\": \"uso\"", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"rawName\": \"estado\"", IntentRewriterSystemPrompt.Text);
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
        Assert.Contains("NUNCA deve ser simplificada dessa forma", IntentRewriterSystemPrompt.Text);
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
        Assert.Contains("attributeHint próprio (ex.: { \"rawName\": \"tipo\", \"rawValue\":", IntentRewriterSystemPrompt.Text);
        Assert.Contains("\"condensador\" }), além de continuar aparecendo em categoryHint", IntentRewriterSystemPrompt.Text);
        Assert.Contains("deduplique um fato removendo-o de attributeHints", IntentRewriterSystemPrompt.Text);
    }
}
