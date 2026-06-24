using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MoviesMafia.Services.Tmdb.Dtos;

namespace MoviesMafia.Services.Tmdb;

/// <summary>
/// Typed HttpClient wrapper for TMDB. Registered via <c>AddHttpClient&lt;ITmdbClient, TmdbClient&gt;</c>.
/// Uses System.Text.Json with snake_case mapping (TMDB returns e.g. "poster_path", "vote_average").
/// </summary>
public sealed class TmdbClient : ITmdbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger<TmdbClient> _logger;
    private readonly TmdbOptions _options;

    public TmdbClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value;
    }

    public Task<MoviePage> DiscoverMoviesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("discover/movie", new() { ["sort_by"] = "popularity.desc", ["page"] = page.ToString() }, ct);

    public Task<MoviePage> DiscoverMoviesAsync(DiscoverFilter filter, int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("discover/movie", BuildDiscoverQuery(filter, page, yearParam: "primary_release_year"), ct);

    public Task<MoviePage> SearchMoviesAsync(string query, int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("search/movie", new() { ["query"] = query, ["page"] = page.ToString() }, ct);

    public Task<VideoPage> GetMovieVideosAsync(int movieId, CancellationToken ct = default) =>
        GetAsync<VideoPage>($"movie/{movieId}/videos", new(), ct);

    public Task<SeriesPage> DiscoverSeriesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("discover/tv", new() { ["sort_by"] = "popularity.desc", ["page"] = page.ToString() }, ct);

    public Task<SeriesPage> DiscoverSeriesAsync(DiscoverFilter filter, int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("discover/tv", BuildDiscoverQuery(filter, page, yearParam: "first_air_date_year"), ct);

    public Task<SeriesPage> SearchSeriesAsync(string query, int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("search/tv", new() { ["query"] = query, ["page"] = page.ToString() }, ct);

    public Task<VideoPage> GetSeriesVideosAsync(int seriesId, CancellationToken ct = default) =>
        GetAsync<VideoPage>($"tv/{seriesId}/videos", new(), ct);

    public async Task<SeriesDetails?> GetSeriesDetailsAsync(int seriesId, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<SeriesDetails>($"tv/{seriesId}", new(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch TMDB series details for {SeriesId}", seriesId);
            return null;
        }
    }

    public Task<MoviePage> GetTrendingMoviesAsync(string window = "day", int page = 1, CancellationToken ct = default)
    {
        var w = string.Equals(window, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "day";
        return GetAsync<MoviePage>($"trending/movie/{w}", new() { ["page"] = page.ToString() }, ct);
    }

    public Task<SeriesPage> GetTrendingSeriesAsync(string window = "day", int page = 1, CancellationToken ct = default)
    {
        var w = string.Equals(window, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "day";
        return GetAsync<SeriesPage>($"trending/tv/{w}", new() { ["page"] = page.ToString() }, ct);
    }

    public Task<MoviePage> GetPopularMoviesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("movie/popular", new() { ["page"] = page.ToString() }, ct);

    public Task<MoviePage> GetTopRatedMoviesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("movie/top_rated", new() { ["page"] = page.ToString() }, ct);

    public Task<MoviePage> GetUpcomingMoviesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("movie/upcoming", new() { ["page"] = page.ToString() }, ct);

    public Task<MoviePage> GetNowPlayingMoviesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<MoviePage>("movie/now_playing", new() { ["page"] = page.ToString() }, ct);

    public Task<SeriesPage> GetPopularSeriesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("tv/popular", new() { ["page"] = page.ToString() }, ct);

    public Task<SeriesPage> GetTopRatedSeriesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("tv/top_rated", new() { ["page"] = page.ToString() }, ct);

    public Task<SeriesPage> GetOnTheAirSeriesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("tv/on_the_air", new() { ["page"] = page.ToString() }, ct);

    public Task<SeriesPage> GetAiringTodaySeriesAsync(int page = 1, CancellationToken ct = default) =>
        GetAsync<SeriesPage>("tv/airing_today", new() { ["page"] = page.ToString() }, ct);

    public string? ImageUrl(string? path, string size = "w500")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return $"{_options.ImageBaseUrl.TrimEnd('/')}/{size}{path}";
    }

    /// <summary>
    /// Translates a <see cref="DiscoverFilter"/> into TMDB discover query params. The year parameter
    /// name differs between movies (<c>primary_release_year</c>) and series (<c>first_air_date_year</c>),
    /// so the caller passes the right one.
    /// </summary>
    private static Dictionary<string, string?> BuildDiscoverQuery(DiscoverFilter filter, int page, string yearParam)
    {
        var query = new Dictionary<string, string?>
        {
            ["sort_by"] = string.IsNullOrWhiteSpace(filter.SortBy) ? "popularity.desc" : filter.SortBy,
            ["page"] = page.ToString(),
            ["vote_count.gte"] = filter.MinVotes.ToString(),
        };

        if (filter.Year is { } year)
        {
            query[yearParam] = year.ToString();
        }

        if (filter.MinRating is { } minRating && minRating > 0)
        {
            query["vote_average.gte"] = minRating.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return query;
    }

    private async Task<T> GetAsync<T>(string path, Dictionary<string, string?> query, CancellationToken ct)
        where T : new()
    {
        query["api_key"] = _options.ApiKey;
        var url = QueryHelpers.AddQueryString(path, query);

        var result = await _http.GetFromJsonAsync<T>(url, JsonOptions, ct);
        return result ?? new T();
    }
}
