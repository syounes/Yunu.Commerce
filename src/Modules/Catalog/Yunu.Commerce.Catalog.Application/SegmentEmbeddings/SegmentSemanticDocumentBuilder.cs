using System.Text;

namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Builds deterministic pt-BR semantic documents for Segment Definitions and
/// Segment Options (docs task: "Implementar sincronização de embeddings de
/// segmentos"). The same source record always produces exactly the same
/// semantic text: field order and formatting are fixed, and only fields that
/// carry semantic meaning are included. SQL Server IDs, AssignmentScope,
/// SelectionMode and IsRequired are never included in the text sent to the
/// embedding model, since those are structural metadata/filters whose changes
/// must not trigger unnecessary provider calls.
/// </summary>
public static class SegmentSemanticDocumentBuilder
{
    public static string BuildDefinitionText(SegmentDefinitionSource definition)
    {
        var sb = new StringBuilder();

        sb.Append("Segmento: ").Append(definition.Name).Append('.');
        sb.Append(" Código: ").Append(definition.Code).Append('.');

        if (!string.IsNullOrWhiteSpace(definition.Description))
        {
            sb.Append(" Descrição: ").Append(definition.Description).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(definition.SemanticText))
        {
            sb.Append(" Significado semântico: ").Append(definition.SemanticText).Append('.');
        }

        return sb.ToString();
    }

    public static string BuildOptionText(SegmentOptionSource option)
    {
        var sb = new StringBuilder();

        sb.Append("Segmento: ").Append(option.SegmentName).Append('.');
        sb.Append(" Código do segmento: ").Append(option.SegmentCode).Append('.');
        sb.Append(" Opção: ").Append(option.OptionName).Append('.');
        sb.Append(" Código da opção: ").Append(option.OptionCode).Append('.');

        if (!string.IsNullOrWhiteSpace(option.OptionDescription))
        {
            sb.Append(" Descrição: ").Append(option.OptionDescription).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(option.OptionSemanticText))
        {
            sb.Append(" Significado semântico: ").Append(option.OptionSemanticText).Append('.');
        }

        return sb.ToString();
    }

    /// <summary>
    /// SHA-256 hash (lowercase hexadecimal) of the exact UTF-8 semantic text
    /// sent to the embedding provider, matching the convention established by
    /// deploy/databases/postgres/005-add-segment-assignment-scope.sql
    /// (encode(digest(convert_to(p_semantic_text, 'UTF8'), 'sha256'), 'hex')).
    /// </summary>
    public static string ComputeContentHash(string semanticText)
    {
        var bytes = Encoding.UTF8.GetBytes(semanticText);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);

        return Convert.ToHexStringLower(hashBytes);
    }

    public static long BuildDefinitionEntityId(long segmentDefinitionId) => segmentDefinitionId;

    public static long BuildOptionEntityId(long segmentOptionId) => segmentOptionId;
}
