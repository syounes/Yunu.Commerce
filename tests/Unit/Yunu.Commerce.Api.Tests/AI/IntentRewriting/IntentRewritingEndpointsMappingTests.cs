using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Api.AI.IntentRewriting;

namespace Yunu.Commerce.Api.Tests.AI.IntentRewriting;

/// <summary>
/// Verifies the HTTP mapping for <see cref="IntentRewriteFailureReason"/>
/// exposed by <see cref="IntentRewritingEndpoints.MapFailureToResult"/> (docs
/// task: "Intent/Query Rewriting" HTTP 503 investigation), in particular that
/// <see cref="IntentRewriteFailureReason.OutputTruncated"/> maps to a
/// specific HTTP 502 Bad Gateway instead of falling back to the generic
/// "unexpected response" HTTP 503.
/// </summary>
public sealed class IntentRewritingEndpointsMappingTests
{
    [Fact]
    public void OutputTruncated_maps_to_specific_HTTP_502()
    {
        var result = IntentRewritingEndpoints.MapFailureToResult(IntentRewriteFailureReason.OutputTruncated);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problemResult.StatusCode);
        Assert.Contains("truncated", problemResult.ProblemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContentFiltered_maps_to_HTTP_422()
    {
        var result = IntentRewritingEndpoints.MapFailureToResult(IntentRewriteFailureReason.ContentFiltered);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problemResult.StatusCode);
    }

    [Theory]
    [InlineData(IntentRewriteFailureReason.Timeout)]
    [InlineData(IntentRewriteFailureReason.ProviderUnavailable)]
    [InlineData(IntentRewriteFailureReason.RateLimited)]
    public void Transient_provider_failures_map_to_HTTP_503(IntentRewriteFailureReason reason)
    {
        var result = IntentRewritingEndpoints.MapFailureToResult(reason);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problemResult.StatusCode);
    }

    [Fact]
    public void InvalidResponse_still_falls_back_to_generic_HTTP_503()
    {
        var result = IntentRewritingEndpoints.MapFailureToResult(IntentRewriteFailureReason.InvalidResponse);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problemResult.StatusCode);
        Assert.Equal(
            "The intent rewriting provider returned an unexpected response.",
            problemResult.ProblemDetails.Detail);
    }
}
