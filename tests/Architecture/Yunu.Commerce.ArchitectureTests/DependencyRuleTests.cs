using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Yunu.Commerce.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture / Hexagonal dependency rules defined in
/// docs/architecture/06-solution-structure.md §27-34 and .github/copilot-instructions.md §4/§37.
/// </summary>
public class DependencyRuleTests
{
    private static readonly string[] BusinessModules =
    {
        "Catalog", "Sellers", "Offers", "Pricing", "Availability", "Fulfillment", "Freight"
    };

    private static readonly string[] SupportModules =
    {
        "Search", "AI", "Integrations"
    };

    private static readonly string[] ForbiddenVendorNamespaces =
    {
        "Microsoft.AspNetCore",
        "MongoDB.Driver",
        "Confluent.Kafka",
        "StackExchange.Redis",
        "Elasticsearch",
        "Nest",
        "Azure.AI",
        "Microsoft.Azure",
        "Google.Cloud",
        "Google.Apis"
    };

    private static Assembly LoadAssembly(string name) => Assembly.Load(name);

    public static IEnumerable<object[]> BusinessModuleNames()
        => BusinessModules.Select(m => new object[] { m });

    public static IEnumerable<object[]> AllModuleNames()
        => BusinessModules.Concat(SupportModules).Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(BusinessModuleNames))]
    public void Domain_Should_Not_Depend_On_Application(string module)
    {
        var domain = LoadAssembly($"Yunu.Commerce.{module}.Domain");

        var result = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOn($"Yunu.Commerce.{module}.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(BusinessModuleNames))]
    public void Domain_Should_Not_Depend_On_Infrastructure(string module)
    {
        var domain = LoadAssembly($"Yunu.Commerce.{module}.Domain");

        var result = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOn($"Yunu.Commerce.{module}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(BusinessModuleNames))]
    public void Domain_Should_Not_Depend_On_Vendor_Infrastructure(string module)
    {
        var domain = LoadAssembly($"Yunu.Commerce.{module}.Domain");

        var result = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenVendorNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(AllModuleNames))]
    public void Application_Should_Not_Depend_On_Infrastructure(string module)
    {
        var application = LoadAssembly($"Yunu.Commerce.{module}.Application");

        var result = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOn($"Yunu.Commerce.{module}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(AllModuleNames))]
    public void Application_Should_Not_Depend_On_Vendor_Infrastructure(string module)
    {
        var application = LoadAssembly($"Yunu.Commerce.{module}.Application");

        var result = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenVendorNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void No_BoundedContext_Domain_Should_Depend_On_Another_BoundedContext_Domain()
    {
        foreach (var module in BusinessModules)
        {
            var domain = LoadAssembly($"Yunu.Commerce.{module}.Domain");

            foreach (var otherModule in BusinessModules.Where(m => m != module))
            {
                var result = Types.InAssembly(domain)
                    .ShouldNot()
                    .HaveDependencyOn($"Yunu.Commerce.{otherModule}")
                    .GetResult();

                Assert.True(result.IsSuccessful, Describe(result));
            }
        }
    }

    [Fact]
    public void Search_Application_Should_Not_Depend_On_Elasticsearch()
    {
        var application = LoadAssembly("Yunu.Commerce.Search.Application");

        var result = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOnAny("Elasticsearch", "Nest")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void AI_Application_Should_Not_Depend_On_Cloud_AI_Providers()
    {
        var application = LoadAssembly("Yunu.Commerce.AI.Application");

        var result = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOnAny("Azure.AI", "Microsoft.Azure", "Google.Cloud", "Google.Apis")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result)
        => result.FailingTypes is null
            ? "Architecture rule violated."
            : "Architecture rule violated by: " + string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
