using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MoviesMafia.Services.Streaming;

/// <summary>
/// Background service that periodically fetches the provider list from <see cref="StreamingOptions.ProvidersUrl"/>
/// (e.g. a GitHub Gist raw URL) and pushes it into <see cref="IProviderCatalog"/>. This lets new
/// providers be added by editing the Gist — no rebuild or redeploy. If the URL is unset, the
/// service does nothing and the inline config providers remain in effect.
/// </summary>
public sealed class ProviderCatalogRefresher : BackgroundService
{
    public const string HttpClientName = "ProviderCatalog";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProviderCatalog _catalog;
    private readonly StreamingOptions _options;
    private readonly ILogger<ProviderCatalogRefresher> _logger;

    public ProviderCatalogRefresher(
        IHttpClientFactory httpClientFactory,
        IProviderCatalog catalog,
        IOptions<StreamingOptions> options,
        ILogger<ProviderCatalogRefresher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _catalog = catalog;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ProvidersUrl))
        {
            _logger.LogInformation("Streaming:ProvidersUrl not set — using inline provider config only.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.ProvidersRefreshMinutes));

        // Fetch once immediately, then on the interval.
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var providers = await client.GetFromJsonAsync<List<StreamingProvider>>(_options.ProvidersUrl!, ct);

            // Keep only well-formed entries (a name and at least one template).
            var valid = (providers ?? new())
                .Where(p => !string.IsNullOrWhiteSpace(p.Name)
                    && (!string.IsNullOrWhiteSpace(p.MovieTemplate) || !string.IsNullOrWhiteSpace(p.EpisodeTemplate)))
                .ToList();

            if (valid.Count == 0)
            {
                _logger.LogWarning("Remote provider list at {Url} was empty/invalid; keeping current list.", _options.ProvidersUrl);
                return;
            }

            _catalog.Set(valid);
            _logger.LogInformation("Loaded {Count} streaming providers from {Url}.", valid.Count, _options.ProvidersUrl);
        }
        catch (Exception ex)
        {
            // Network blip, bad JSON, etc. — keep whatever list we already have.
            _logger.LogWarning(ex, "Failed to refresh providers from {Url}; keeping current list.", _options.ProvidersUrl);
        }
    }
}
