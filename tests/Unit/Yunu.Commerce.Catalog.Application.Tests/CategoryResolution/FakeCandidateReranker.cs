using Yunu.Commerce.AI.Application.Reranking;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

/// <summary>
/// Test-only fake for ICandidateReranker (docs task: "Contextual candidate
/// reranking"). Returns a preconfigured result or throws a preconfigured
/// technical failure, and records the last request received so tests can
/// assert what was sent to the reranker (never an official ID, only
/// Index/DisplayText/Metadata).
/// </summary>
internal sealed class FakeCandidateReranker : ICandidateReranker
{
    private readonly CandidateRerankResult? _result;
    private readonly CandidateRerankException? _exception;

    private FakeCandidateReranker(CandidateRerankResult? result, CandidateRerankException? exception)
    {
        _result = result;
        _exception = exception;
    }

    public static FakeCandidateReranker Returning(CandidateRerankResult result) => new(result, null);

    public static FakeCandidateReranker Throwing(CandidateRerankException exception) => new(null, exception);

    public CandidateRerankRequest? LastRequest { get; private set; }

    public int CallCount { get; private set; }

    public Task<CandidateRerankResult> RerankAsync(CandidateRerankRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        CallCount++;

        if (_exception is not null)
        {
            throw _exception;
        }

        return Task.FromResult(_result!);
    }
}
