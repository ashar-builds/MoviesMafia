using Microsoft.Extensions.Options;

namespace MoviesMafia.Services.Streaming;

/// <summary>
/// Builds auto-embed URLs in the provider's expected shape:
/// movies as <c>{base}/movie/tmdb/{id}</c> and episodes as <c>{base}/tv/tmdb/{id}-{season}-{episode}</c>.
/// </summary>
public sealed class EmbedUrlBuilder : IEmbedUrlBuilder
{
    private readonly StreamingOptions _options;

    public EmbedUrlBuilder(IOptions<StreamingOptions> options) => _options = options.Value;

    public string Movie(int tmdbId) => $"{Base}/movie/tmdb/{tmdbId}";

    public string Episode(int tmdbId, int season, int episode) =>
        $"{Base}/tv/tmdb/{tmdbId}-{season}-{episode}";

    private string Base => _options.AutoEmbedUrl.TrimEnd('/');
}
