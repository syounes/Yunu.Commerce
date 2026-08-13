using System.Net.Http;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy;

/// <summary>
/// HTTP-based adapter implementing <see cref="IGoogleTaxonomySource"/>, downloading
/// the official Google Product Taxonomy text feed (docs task: Google source client).
/// Uses <see cref="IHttpClientFactory"/> exclusively; never instantiates HttpClient
/// directly (docs §31/§45).
/// </summary>
public sealed class GoogleTaxonomyHttpSource : IGoogleTaxonomySource
{
    internal const string HttpClientName = "GoogleTaxonomySource";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleTaxonomyOptions _options;

    public GoogleTaxonomyHttpSource(IHttpClientFactory httpClientFactory, IOptions<GoogleTaxonomyOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<string>> GetTaxonomyAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await httpClient.GetAsync(_options.SourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new GoogleTaxonomyValidationException("The downloaded Google taxonomy feed was empty.");
        }

        return content
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
    }
}
