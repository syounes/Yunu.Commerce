using System.Globalization;
using System.Text;

namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Builds deterministic pt-BR semantic documents for Attribute Definitions and
/// Attribute Options (docs task: "SKU attribute embedding synchronization
/// pipeline"). The same source record always produces exactly the same
/// semantic text: field order and formatting are fixed, and only fields that
/// carry semantic meaning are included. SQL Server IDs are never included in
/// the text sent to the embedding model.
/// </summary>
public static class AttributeSemanticDocumentBuilder
{
    public static string BuildDefinitionText(AttributeDefinitionSource definition)
    {
        var sb = new StringBuilder();

        sb.Append("Atributo: ").Append(definition.Name).Append('.');
        sb.Append(" Código: ").Append(definition.Code).Append('.');

        if (!string.IsNullOrWhiteSpace(definition.GoogleAttributeName))
        {
            sb.Append(" Nome Google: ").Append(definition.GoogleAttributeName).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(definition.Description))
        {
            sb.Append(" Descrição: ").Append(definition.Description).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(definition.SemanticText))
        {
            sb.Append(" Significado semântico: ").Append(definition.SemanticText).Append('.');
        }

        sb.Append(" Tipo de dado: ").Append(definition.DataType).Append('.');
        sb.Append(" Cardinalidade: ").Append(definition.Cardinality).Append('.');

        sb.Append(" Família de unidade: ")
          .Append(string.IsNullOrWhiteSpace(definition.UnitFamily) ? "não aplicável" : definition.UnitFamily)
          .Append('.');

        sb.Append(" Eixo de variante: ").Append(definition.IsVariantAxis ? "sim" : "não").Append('.');
        sb.Append(" Pesquisável: ").Append(definition.IsSearchable ? "sim" : "não").Append('.');
        sb.Append(" Filtrável: ").Append(definition.IsFilterable ? "sim" : "não").Append('.');
        sb.Append(" Obrigatório por padrão: ").Append(definition.IsRequiredByDefault ? "sim" : "não").Append('.');

        return sb.ToString();
    }

    public static string BuildOptionText(AttributeOptionSource option)
    {
        var sb = new StringBuilder();

        sb.Append("Atributo: ").Append(option.AttributeName).Append('.');
        sb.Append(" Código do atributo: ").Append(option.AttributeCode).Append('.');
        sb.Append(" Opção: ").Append(option.OptionName).Append('.');
        sb.Append(" Código da opção: ").Append(option.OptionCode).Append('.');

        if (!string.IsNullOrWhiteSpace(option.GoogleValue))
        {
            sb.Append(" Valor Google Merchant: ").Append(option.GoogleValue).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(option.OptionSemanticText))
        {
            sb.Append(" Significado semântico: ").Append(option.OptionSemanticText).Append('.');
        }

        return sb.ToString();
    }

    /// <summary>
    /// SHA-256 hash (lowercase hexadecimal) of the exact UTF-8 semantic text
    /// sent to the embedding provider, matching the convention already
    /// established by deploy/databases/postgres/003_create_sku_attribute_vectors.sql
    /// (encode(digest(...,'sha256'),'hex')).
    /// </summary>
    public static string ComputeContentHash(string semanticText)
    {
        var bytes = Encoding.UTF8.GetBytes(semanticText);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);

        return Convert.ToHexStringLower(hashBytes);
    }

    public static string BuildDefinitionEntityId(string attributeCode) => attributeCode;

    public static string BuildOptionEntityId(string attributeCode, string optionCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{attributeCode}:{optionCode}");
}
