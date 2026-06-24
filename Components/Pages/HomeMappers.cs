using MoviesMafia.Components.Shared;
using MoviesMafia.Services.Tmdb;
using MoviesMafia.Services.Tmdb.Dtos;

namespace MoviesMafia.Components.Pages;

/// <summary>Shared mappers used by the discovery pages (Home / Movies / Series).</summary>
internal static class HomeMappers
{
    internal const int RowSize = 18;
    internal const int HeroSize = 6;

    internal static string? Year(string? date) =>
        !string.IsNullOrEmpty(date) && date.Length >= 4 ? date[..4] : null;

    internal static List<HeroSlide> ToHeroSlides(
        IEnumerable<MovieResult> movies, ITmdbClient tmdb, string kind = "Trending movie") =>
        movies.Where(m => !string.IsNullOrWhiteSpace(m.BackdropPath))
              .Take(HeroSize)
              .Select(m => new HeroSlide(
                  Kind: kind,
                  Title: m.Title ?? m.OriginalTitle ?? "Untitled",
                  Overview: m.Overview,
                  Year: Year(m.ReleaseDate),
                  Score: m.VoteAverage,
                  BackdropUrl: tmdb.ImageUrl(m.BackdropPath, "original"),
                  WatchUrl: $"/watch/movie/{m.Id}",
                  TrailerUrl: $"/trailer/movie/{m.Id}"))
              .ToList();

    internal static List<HeroSlide> ToHeroSlides(
        IEnumerable<SeriesResult> series, ITmdbClient tmdb, string kind = "Trending series") =>
        series.Where(s => !string.IsNullOrWhiteSpace(s.BackdropPath))
              .Take(HeroSize)
              .Select(s => new HeroSlide(
                  Kind: kind,
                  Title: s.Name ?? s.OriginalName ?? "Untitled",
                  Overview: s.Overview,
                  Year: Year(s.FirstAirDate),
                  Score: s.VoteAverage,
                  BackdropUrl: tmdb.ImageUrl(s.BackdropPath, "original"),
                  WatchUrl: $"/watch/series/{s.Id}",
                  TrailerUrl: $"/trailer/series/{s.Id}"))
              .ToList();
}
