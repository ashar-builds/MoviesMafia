namespace MoviesMafia.Components.Shared;

/// <summary>A single slide rendered by <see cref="HeroCarousel"/>.</summary>
public sealed record HeroSlide(
    string Kind,
    string Title,
    string? Overview,
    string? Year,
    double Score,
    string? BackdropUrl,
    string WatchUrl,
    string TrailerUrl);
