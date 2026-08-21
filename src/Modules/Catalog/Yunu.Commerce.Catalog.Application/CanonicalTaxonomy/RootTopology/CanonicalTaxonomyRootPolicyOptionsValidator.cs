using Microsoft.Extensions.Options;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Validates <see cref="CanonicalTaxonomyRootPolicyOptions"/> at startup
/// (ValidateOnStart), so a misconfigured Root Topology Policy fails fast
/// instead of at first audit (docs task: "Root Topology Policy"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeResolutionOptionsValidator"/>.
/// </summary>
public sealed class CanonicalTaxonomyRootPolicyOptionsValidator : IValidateOptions<CanonicalTaxonomyRootPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, CanonicalTaxonomyRootPolicyOptions options)
    {
        if (!Enum.IsDefined(options.RootMode))
        {
            return ValidateOptionsResult.Fail(
                "Catalog:CanonicalTaxonomy:RootTopology:RootMode must be a supported CanonicalTaxonomyRootMode value.");
        }

        if (options.RootMode == CanonicalTaxonomyRootMode.SingleRoot)
        {
            if (string.IsNullOrWhiteSpace(options.PrimaryRootCode))
            {
                return ValidateOptionsResult.Fail(
                    "Catalog:CanonicalTaxonomy:RootTopology:PrimaryRootCode is required when RootMode is SingleRoot.");
            }

            if (string.IsNullOrWhiteSpace(options.PrimaryRootName))
            {
                return ValidateOptionsResult.Fail(
                    "Catalog:CanonicalTaxonomy:RootTopology:PrimaryRootName is required when RootMode is SingleRoot.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
