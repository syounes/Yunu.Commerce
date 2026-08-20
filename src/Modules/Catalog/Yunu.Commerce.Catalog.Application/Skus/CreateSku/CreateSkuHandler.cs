using System.Globalization;
using Yunu.Commerce.Catalog.Application.AttributeCatalog;
using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Orchestrates creation of a new Sku Aggregate (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Business invariants are enforced entirely by Catalog.Domain (Sku.Create and its
/// Value Objects); this handler performs only mapping and persistence orchestration.
///
/// Product existence is validated before creating the Sku to prevent orphan SKUs.
/// This is an Application-level cross-Aggregate validation, not a Domain invariant
/// (the Sku Aggregate itself does not enforce Product existence).
///
/// The final persistence step goes through
/// <see cref="IProductSkuConcurrencyCoordinator"/> rather than a plain
/// <see cref="ISkuRepository.AddAsync"/> call
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md,
/// "V11" cross-aggregate concurrency): a concurrent ArchiveProduct racing
/// against this creation could otherwise both pass their own guard check
/// against a state that changes before the other commits (write skew),
/// leaving an Archived Product with a non-Archived Sku. The coordinator
/// atomically re-verifies the Product is not Archived and creates the Sku
/// inside the same MongoDB transaction.
///
/// Explicit, structured attribute assignments (docs task: "SKU attribute
/// foundation") are resolved and validated against SQL Server
/// (<see cref="IAttributeCatalogRepository"/>) BEFORE Sku.AssignAttribute is
/// called: unknown/inactive definitions are rejected, values are converted
/// according to the definition's DataType and constrained by ValidationRegex/
/// MinNumericValue/MaxNumericValue/MaxLength, and Enum attributes resolve
/// their AttributeOption by definition + option code. This stage does not
/// interpret natural-language phrases; the caller must send explicit
/// attribute codes and values.
/// </summary>
public sealed class CreateSkuHandler
{
    private readonly IProductRepository _productRepository;
    private readonly IAttributeCatalogRepository _attributeCatalogRepository;
    private readonly IProductSkuConcurrencyCoordinator _concurrencyCoordinator;

    public CreateSkuHandler(
        IProductRepository productRepository,
        IAttributeCatalogRepository attributeCatalogRepository,
        IProductSkuConcurrencyCoordinator concurrencyCoordinator)
    {
        _productRepository = productRepository;
        _attributeCatalogRepository = attributeCatalogRepository;
        _concurrencyCoordinator = concurrencyCoordinator;
    }

    public async Task<CreateSkuResult> HandleAsync(CreateSkuCommand command, CancellationToken cancellationToken)
    {
        var productId = new ProductId(command.ProductId);

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException($"Product with ID '{command.ProductId}' does not exist. Cannot create Sku for non-existent Product.");
        }

        if (product.Status == ProductStatus.Archived)
        {
            throw new InvalidOperationException($"Product with ID '{command.ProductId}' is Archived. Cannot create a Sku under an Archived Product.");
        }

        var skuId = SkuId.New();
        var code = new SkuCode(command.Code);

        var sku = Sku.Create(skuId, productId, code, command.Gtin);

        foreach (var attributeInput in command.Attributes)
        {
            await AssignAttributeAsync(sku, attributeInput, cancellationToken);
        }

        var result = await _concurrencyCoordinator.CreateSkuIfProductNotArchivedAsync(sku, cancellationToken);

        switch (result)
        {
            case CreateSkuCoordinationResult.Created:
                break;
            case CreateSkuCoordinationResult.ProductNotFound:
                throw new InvalidOperationException($"Product with ID '{command.ProductId}' does not exist. Cannot create Sku for non-existent Product.");
            case CreateSkuCoordinationResult.ProductArchived:
                throw new InvalidOperationException($"Product with ID '{command.ProductId}' is Archived. Cannot create a Sku under an Archived Product.");
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported CreateSku coordination result.");
        }

        return new CreateSkuResult
        {
            SkuId = skuId.Value,
            Attributes = sku.Attributes.Select(SkuAttributeResponseMapper.ToResponse).ToArray()
        };
    }

    private async Task AssignAttributeAsync(Sku sku, SkuAttributeInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Code))
        {
            throw new ArgumentException("Attribute code cannot be null, empty or whitespace.", nameof(input));
        }

        var definition = await _attributeCatalogRepository.GetDefinitionByCodeAsync(input.Code, cancellationToken);

        if (definition is null)
        {
            throw new ArgumentException($"Attribute '{input.Code}' does not exist.", nameof(input));
        }

        if (!definition.IsActive)
        {
            throw new ArgumentException($"Attribute '{input.Code}' is not active.", nameof(input));
        }

        var attributeDefinitionId = new AttributeDefinitionId(definition.AttributeDefinitionId);
        var dataType = ParseDataType(definition.DataType);

        AttributeOptionId? attributeOptionId = null;
        SkuAttributeValue value;

        if (dataType == SkuAttributeDataType.Enum)
        {
            if (string.IsNullOrWhiteSpace(input.OptionCode))
            {
                throw new ArgumentException($"Attribute '{input.Code}' requires an option code.", nameof(input));
            }

            var option = await _attributeCatalogRepository.GetOptionAsync(definition.AttributeDefinitionId, input.OptionCode, cancellationToken);

            if (option is null)
            {
                throw new ArgumentException($"'{input.OptionCode}' is not a valid option for attribute '{input.Code}'.", nameof(input));
            }

            if (!option.IsActive)
            {
                throw new ArgumentException($"Option '{input.OptionCode}' for attribute '{input.Code}' is not active.", nameof(input));
            }

            attributeOptionId = new AttributeOptionId(option.AttributeOptionId);
            value = SkuAttributeValue.ForEnum(option.Code, input.OptionCode);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.Value))
            {
                throw new ArgumentException($"Attribute '{input.Code}' requires a value.", nameof(input));
            }

            value = ConvertValue(definition, dataType, input.Value);
        }

        sku.AssignAttribute(attributeDefinitionId, definition.Code, input.Sequence, value, attributeOptionId);
    }

    private static SkuAttributeValue ConvertValue(AttributeDefinitionResponse definition, SkuAttributeDataType dataType, string rawValue)
    {
        switch (dataType)
        {
            case SkuAttributeDataType.Text:
                ValidateText(definition, rawValue);
                return SkuAttributeValue.ForText(rawValue);

            case SkuAttributeDataType.Integer:
            {
                if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
                {
                    throw new ArgumentException($"'{rawValue}' is not a valid integer for attribute '{definition.Code}'.");
                }

                ValidateNumericRange(definition, integerValue);
                return SkuAttributeValue.ForInteger(integerValue, rawValue);
            }

            case SkuAttributeDataType.Decimal:
            {
                if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    throw new ArgumentException($"'{rawValue}' is not a valid decimal for attribute '{definition.Code}'.");
                }

                ValidateNumericRange(definition, decimalValue);
                return SkuAttributeValue.ForDecimal(decimalValue, rawValue);
            }

            case SkuAttributeDataType.Boolean:
            {
                if (!bool.TryParse(rawValue, out var booleanValue))
                {
                    throw new ArgumentException($"'{rawValue}' is not a valid boolean for attribute '{definition.Code}'.");
                }

                return SkuAttributeValue.ForBoolean(booleanValue, rawValue);
            }

            case SkuAttributeDataType.DateTime:
            {
                if (!System.DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeValue))
                {
                    throw new ArgumentException($"'{rawValue}' is not a valid date/time for attribute '{definition.Code}'.");
                }

                return SkuAttributeValue.ForDateTime(dateTimeValue, rawValue);
            }

            case SkuAttributeDataType.Money:
            {
                var parts = rawValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length != 2 || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    throw new ArgumentException($"'{rawValue}' is not a valid money value for attribute '{definition.Code}'. Expected format: '<amount> <ISO currency code>'.");
                }

                return SkuAttributeValue.ForMoney(amount, parts[1], rawValue);
            }

            case SkuAttributeDataType.Measurement:
            {
                var parts = rawValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length != 2 || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var measurementValue))
                {
                    throw new ArgumentException($"'{rawValue}' is not a valid measurement value for attribute '{definition.Code}'. Expected format: '<value> <unit code>'.");
                }

                return SkuAttributeValue.ForMeasurement(measurementValue, parts[1], rawValue);
            }

            case SkuAttributeDataType.Url:
                return SkuAttributeValue.ForUrl(rawValue);

            case SkuAttributeDataType.Json:
                return SkuAttributeValue.ForJson(rawValue);

            default:
                throw new ArgumentException($"Unsupported attribute DataType '{dataType}' for attribute '{definition.Code}'.");
        }
    }

    private static void ValidateText(AttributeDefinitionResponse definition, string rawValue)
    {
        if (definition.MaxLength is { } maxLength && rawValue.Length > maxLength)
        {
            throw new ArgumentException($"Attribute '{definition.Code}' value exceeds the maximum length of {maxLength}.");
        }

        if (!string.IsNullOrWhiteSpace(definition.ValidationRegex) &&
            !System.Text.RegularExpressions.Regex.IsMatch(rawValue, definition.ValidationRegex))
        {
            throw new ArgumentException($"Attribute '{definition.Code}' value does not match the required format.");
        }
    }

    private static void ValidateNumericRange(AttributeDefinitionResponse definition, decimal value)
    {
        if (definition.MinNumericValue is { } min && value < min)
        {
            throw new ArgumentException($"Attribute '{definition.Code}' value must be greater than or equal to {min}.");
        }

        if (definition.MaxNumericValue is { } max && value > max)
        {
            throw new ArgumentException($"Attribute '{definition.Code}' value must be less than or equal to {max}.");
        }
    }

    private static SkuAttributeDataType ParseDataType(string dataType)
    {
        if (!Enum.TryParse<SkuAttributeDataType>(dataType, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Unknown attribute DataType '{dataType}'.");
        }

        return parsed;
    }
}

