using MoviesMafia.Services.Tmdb.Dtos;

namespace MoviesMafia.Services.Tmdb;

/// <summary>Typed client over the TMDB REST API. Implementations throw on transport/HTTP failure.</summary>
public interface ITmdbClient
{
    Task<MoviePage> DiscoverMoviesAsync(int page = 1, CancellationToken ct = default);
    Task<MoviePage> SearchMoviesAsync(string query, int page = 1, CancellationToken ct = default);
    Task<VideoPage> GetMovieVideosAsync(int movieId, CancellationToken ct = default);

    Task<SeriesPage> DiscoverSeriesAsync(int page = 1, CancellationToken ct = default);
    Task<SeriesPage> SearchSeriesAsync(string query, int page = 1, CancellationToken ct = default);
    Task<VideoPage> GetSeriesVideosAsync(int seriesId, CancellationToken ct = default);
    Task<SeriesDetails?> GetSeriesDetailsAsync(int seriesId, CancellationToken ct = default);

    /// <summary>Trending movies for the given window ("day" or "week").</summary>
    Task<MoviePage> GetTrendingMoviesAsync(string window = "day", int page = 1, CancellationToken ct = default);

    /// <summary>Trending series for the given window ("day" or "week").</summary>
    Task<SeriesPage> GetTrendingSeriesAsync(string window = "day", int page = 1, CancellationToken ct = default);

    Task<MoviePage> GetPopularMoviesAsync(int page = 1, CancellationToken ct = default);
    Task<MoviePage> GetTopRatedMoviesAsync(int page = 1, CancellationToken ct = default);
    Task<MoviePage> GetUpcomingMoviesAsync(int page = 1, CancellationToken ct = default);
    Task<MoviePage> GetNowPlayingMoviesAsync(int page = 1, CancellationToken ct = default);

    Task<SeriesPage> GetPopularSeriesAsync(int page = 1, CancellationToken ct = default);
    Task<SeriesPage> GetTopRatedSeriesAsync(int page = 1, CancellationToken ct = default);
    Task<SeriesPage> GetOnTheAirSeriesAsync(int page = 1, CancellationToken ct = default);
    Task<SeriesPage> GetAiringTodaySeriesAsync(int page = 1, CancellationToken ct = default);

    /// <summary>Builds a full image URL for a TMDB poster/backdrop path, or null when the path is empty.</summary>
    string? ImageUrl(string? path, string size = "w500");
}
