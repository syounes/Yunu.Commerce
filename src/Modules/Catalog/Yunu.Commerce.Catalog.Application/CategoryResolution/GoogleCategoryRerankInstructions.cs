namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Google-Category-specific hardening instructions for the contextual
/// candidate reranker (docs task: "Google Category reranking hardening").
/// This text is sent only as the per-request <c>Task</c> of the shared,
/// generic <see cref="Yunu.Commerce.AI.Application.Reranking.ICandidateReranker"/>
/// (see <see cref="Yunu.Commerce.AI.Application.Reranking.CandidateRerankRequest.Task"/>),
/// never added to the shared/global reranker system prompt (<see
/// cref="Yunu.Commerce.AI.Infrastructure.Reranking.AzureOpenAI.CandidateRerankerSystemPrompt"/>),
/// which is also used, unmodified, by AttributeDefinition and AttributeOption
/// reranking. Deliberately generic across products/categories: it never
/// hardcodes a specific term (e.g. "tênis") or a specific GoogleCategoryId;
/// it only describes the general reasoning the reranker must apply.
/// </summary>
public static class GoogleCategoryRerankInstructions
{
    public const string Task = """
        Selecione, exclusivamente entre os candidatos fornecidos, a categoria do
        Google Product Taxonomy que melhor representa a natureza física e
        comercial do produto descrito pelo usuário.

        Analise conjuntamente: a intenção original, a consulta normalizada, a
        consulta semântica, o categoryHint, o categorySearchQuery, os atributos
        explícitos, o nome de cada candidato, o caminho taxonômico completo, a
        profundidade e a similaridade vetorial (todos fornecidos no contexto,
        quando disponíveis).

        Não selecione uma categoria apenas porque o nome da folha coincide
        lexicalmente com uma palavra da consulta. O nome da folha nunca deve
        ser analisado isoladamente: considere sempre todos os ancestrais
        presentes no caminho taxonômico de cada candidato.

        Antes de selecionar, identifique qual é o objeto físico comercializado.
        Diferencie explicitamente: produto físico, atividade, modalidade
        esportiva, acessório, peça, equipamento, vestuário, calçado, serviço e
        conteúdo digital.

        Quando um termo possuir mais de um significado, use o contexto completo
        e os atributos explícitos como evidência de desambiguação. Por exemplo,
        gênero, numeração, sistema de tamanho, cor e material associados a um
        termo de calçado indicam o produto físico calçado, enquanto raquete,
        bola, rede ou equipamento para a prática de um esporte associados ao
        mesmo termo indicam um artigo da modalidade esportiva. Esta é uma regra
        semântica geral: aplique o mesmo raciocínio a qualquer termo
        polissêmico, nunca memorizando um caso específico como exceção fixa.

        Prefira a categoria que representa o que o produto é, e não apenas onde
        ele é usado, para qual atividade ele serve, uma palavra semelhante, uma
        característica, um público, um material ou um benefício.

        A similaridade vetorial é um sinal de recuperação, não uma decisão
        final: um candidato com similaridade ligeiramente maior pode estar
        semanticamente incorreto.

        Não invente categorias e não selecione índices que não estejam na
        lista de candidatos recebida.

        Se nenhum candidato representar adequadamente a natureza física e
        comercial do produto, não force uma escolha: retorne decision = "None"
        (ausência de candidato adequado) ou decision = "Ambiguous" (mais de um
        candidato plausível), conforme o contrato disponível.
        """;
}
