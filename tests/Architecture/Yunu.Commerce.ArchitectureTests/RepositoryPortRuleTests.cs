using System.Reflection;
using Xunit;

namespace Yunu.Commerce.ArchitectureTests;

/// <summary>
/// Enforces boundary rules specific to Domain-level repository ports, starting with
/// Catalog's IProductRepository (docs/domains/catalog.md §40-41,
/// docs/adr/0001-use-ddd-clean-hexagonal.md §9/§11,
/// docs/adr/0003-database-per-bounded-context.md).
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

    [Fact]
    public void IProductRepository_Should_Exist_In_Catalog_Domain_Assembly()
    {
        var domainAssembly = Assembly.Load("Yunu.Commerce.Catalog.Domain");

        var repositoryType = domainAssembly.GetType("Yunu.Commerce.Catalog.Domain.Products.IProductRepository");

        Assert.NotNull(repositoryType);
        Assert.True(repositoryType!.IsInterface);
    }

    [Fact]
    public void IProductRepository_Should_Not_Exist_In_Catalog_Application_Or_Infrastructure_Or_Contracts()
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

            var repositoryType = assembly.GetType("Yunu.Commerce.Catalog.Domain.Products.IProductRepository");

            Assert.Null(repositoryType);
        }
    }

    [Fact]
    public void IProductRepository_Methods_Should_Not_Reference_Forbidden_Vendor_Namespaces()
    {
        var domainAssembly = Assembly.Load("Yunu.Commerce.Catalog.Domain");
        var repositoryType = domainAssembly.GetType("Yunu.Commerce.Catalog.Domain.Products.IProductRepository");

        Assert.NotNull(repositoryType);

        foreach (var method in repositoryType!.GetMethods())
        {
            AssertTypeIsNotVendorSpecific(method.ReturnType, method);

            foreach (var parameter in method.GetParameters())
            {
                AssertTypeIsNotVendorSpecific(parameter.ParameterType, method);
            }
        }
    }

    [Fact]
    public void IProductRepository_Should_Not_Be_A_Generic_Repository_Abstraction()
    {
        var domainAssembly = Assembly.Load("Yunu.Commerce.Catalog.Domain");
        var repositoryType = domainAssembly.GetType("Yunu.Commerce.Catalog.Domain.Products.IProductRepository");

        Assert.NotNull(repositoryType);

        Assert.False(
            repositoryType!.IsGenericType || repositoryType.IsGenericTypeDefinition,
            "IProductRepository must not be a generic repository abstraction (e.g. IRepository<T>).");

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

    private static void AssertTypeIsNotVendorSpecific(Type type, MethodInfo method)
    {
        var underlyingType = UnwrapTaskType(type);
        var fullName = underlyingType.FullName ?? underlyingType.Name;

        foreach (var forbiddenPrefix in ForbiddenVendorNamespacePrefixes)
        {
            Assert.False(
                fullName.StartsWith(forbiddenPrefix, StringComparison.Ordinal),
                $"Method '{method.Name}' on IProductRepository references forbidden vendor type '{fullName}'.");
        }
    }

    private static Type UnwrapTaskType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }
}
