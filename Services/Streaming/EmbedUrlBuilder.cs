using Microsoft.Extensions.Options;

namespace MoviesMafia.Services.Streaming;

/// <summary>
/// Builds embed URLs for every configured <see cref="StreamingProvider"/> by substituting the
/// <c>{id}</c>, <c>{season}</c> and <c>{episode}</c> placeholders in each provider's template.
/// </summary>
public sealed class EmbedUrlBuilder : IEmbedUrlBuilder
{
    private readonly StreamingOptions _options;

    public EmbedUrlBuilder(IOptions<StreamingOptions> options) => _options = options.Value;

    public IReadOnlyList<EmbedSource> Movie(int tmdbId) =>
        Build(p => p.MovieTemplate, new()
        {
            ["{id}"] = tmdbId.ToString(),
        });

    public IReadOnlyList<EmbedSource> Episode(int tmdbId, int season, int episode) =>
        Build(p => p.EpisodeTemplate, new()
        {
            ["{id}"] = tmdbId.ToString(),
            ["{season}"] = season.ToString(),
            ["{episode}"] = episode.ToString(),
        });

    private List<EmbedSource> Build(Func<StreamingProvider, string> template, Dictionary<string, string> tokens)
    {
        var sources = new List<EmbedSource>(_options.Providers.Count);
        foreach (var provider in _options.Providers)
        {
            var url = template(provider);
            if (string.IsNullOrWhiteSpace(url)) continue; // provider doesn't support this media type

            foreach (var (placeholder, value) in tokens)
                url = url.Replace(placeholder, value);

            var name = string.IsNullOrWhiteSpace(provider.Name) ? "Source" : provider.Name;
            sources.Add(new EmbedSource(name, url));
        }
        return sources;
    }
}
