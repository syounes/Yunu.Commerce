using System.Reflection;
using Xunit;

namespace Yunu.Commerce.ArchitectureTests;

/// <summary>
/// Enforces boundary rules specific to Domain-level repository ports, covering
/// Catalog's IProductRepository and ISkuRepository (docs/domains/catalog.md §40-41,
/// docs/adr/0001-use-ddd-clean-hexagonal.md §9/§11,
/// docs/adr/0003-database-per-bounded-context.md,
/// docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// </summary>
public class RepositoryPortRuleTests
{
    private static readonly string[] ForbiddenVendorNamespacePrefixes =
    {
        "MongoDB",
        "Microsoft.EntityFrameworkCore",
        "Dapper",
        "StackExchange.Redis",
        "Elasticsearch",
        "Nest",
        "Confluent.Kafka",
        "Azure",
        "Google"
    };

    private const string ProductRepositoryTypeName = "Yunu.Commerce.Catalog.Domain.Products.IProductRepository";
    private const string SkuRepositoryTypeName = "Yunu.Commerce.Catalog.Domain.Skus.ISkuRepository";

    public static IEnumerable<object[]> RepositoryPortTypeNames() => new[]
    {
        new object[] { ProductRepositoryTypeName },
        new object[] { SkuRepositoryTypeName }
    };

    [Theory]
    [MemberData(nameof(RepositoryPortTypeNames))]
    public void RepositoryPort_Should_Exist_In_Catalog_Domain_Assembly(string repositoryTypeName)
    {
        var domainAssembly = Assembly.Load("Yunu.Commerce.Catalog.Domain");

        var repositoryType = domainAssembly.GetType(repositoryTypeName);

        Assert.NotNull(repositoryType);
        Assert.True(repositoryType!.IsInterface);
    }

    [Theory]
    [MemberData(nameof(RepositoryPortTypeNames))]
    public void RepositoryPort_Should_Not_Exist_In_Catalog_Application_Or_Infrastructure_Or_Contracts(string repositoryTypeName)
    {
        var forbiddenAssemblyNames = new[]
        {
            "Yunu.Commerce.Catalog.Application",
            "Yunu.Commerce.Catalog.Infrastructure",
            "Yunu.Commerce.Catalog.Contracts"
        };

        foreach (var assemblyName in forbiddenAssemblyNames)
        {
            var assembly = Assembly.Load(assemblyName);

            var repositoryType = assembly.GetType(repositoryTypeName);

            Assert.Null(repositoryType);
        }
    }

    [Theory]
    [MemberData(nameof(RepositoryPortTypeNames))]
    public void RepositoryPort_Methods_Should_Not_Reference_Forbidden_Vendor_Namespaces(string repositoryTypeName)
    {
        var domainAssembly = Assembly.Load("Yunu.Commerce.Catalog.Domain");
        var repositoryType = domainAssembly.GetType(repositoryTypeName);

        Assert.NotNull(repositoryType);

        foreach (var method in repositoryType!.GetMethods())
        {
            AssertTypeIsNotVendorSpecific(type: method.ReturnType, method: method, repositoryTypeName: repositoryTypeName);

            foreach (var parameter in method.GetParameters())
            {
                AssertTypeIsNotVendorSpecific(type: parameter.ParameterType, method: method, repositoryTypeName: repositoryTypeName);
            }
        }
    }

    [Theory]
    [MemberData(nameof(RepositoryPortTypeNames))]
    public void RepositoryPort_Should_Not_Be_A_Generic_Repository_Abstraction(string repositoryTypeName)
    {
        var domainAssembly = Assembly.Load("Yunu.Commerce.Catalog.Domain");
        var repositoryType = domainAssembly.GetType(repositoryTypeName);

        Assert.NotNull(repositoryType);

        Assert.False(
            repositoryType!.IsGenericType || repositoryType.IsGenericTypeDefinition,
            $"{repositoryTypeName} must not be a generic repository abstraction (e.g. IRepository<T>).");

        var methodNames = repositoryType.GetMethods().Select(m => m.Name).ToArray();

        var forbiddenMethodNames = new[]
        {
            "UpdateAsync",
            "SaveAsync",
            "DeleteAsync",
            "ExistsAsync",
            "GetAllAsync",
            "SearchAsync"
        };

        foreach (var forbidden in forbiddenMethodNames)
        {
            Assert.DoesNotContain(forbidden, methodNames);
        }
    }

    private static void AssertTypeIsNotVendorSpecific(Type type, MethodInfo method, string repositoryTypeName)
    {
        var underlyingType = UnwrapTaskType(type);
        var fullName = underlyingType.FullName ?? underlyingType.Name;

        foreach (var forbiddenPrefix in ForbiddenVendorNamespacePrefixes)
        {
            Assert.False(
                fullName.StartsWith(forbiddenPrefix, StringComparison.Ordinal),
                $"Method '{method.Name}' on {repositoryTypeName} references forbidden vendor type '{fullName}'.");
        }
    }

    private static Type UnwrapTaskType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return type.GetGenericArguments()[0];
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }
}
