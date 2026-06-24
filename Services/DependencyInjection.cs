using Microsoft.EntityFrameworkCore;
using MoviesMafia.Data;
using MoviesMafia.Services.Email;
using MoviesMafia.Services.Options;
using MoviesMafia.Services.Repositories;
using MoviesMafia.Services.Storage;
using MoviesMafia.Services.Streaming;
using MoviesMafia.Services.Tmdb;

namespace MoviesMafia.Services;

public static class DependencyInjection
{
    /// <summary>Registers data access, options, and application services for the web host.</summary>
    public static IServiceCollection AddMoviesMafiaServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();

        // Strongly-typed, validated options.
        services.AddOptions<TmdbOptions>().BindConfiguration(TmdbOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
        services.AddOptions<StreamingOptions>().BindConfiguration(StreamingOptions.SectionName);
        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName);
        services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);

        // Database.
        //
        // Register a DbContext *factory* so repositories can each own a short-lived context.
        // Under Blazor Static SSR + ReactiveBlazor, multiple components on one page (plus
        // out-of-band signal re-renders) execute concurrently; a single shared scoped context
        // throws "A second operation was started on this context instance...". The factory
        // hands out isolated contexts per operation and avoids that race.
        //
        // ASP.NET Identity still needs a scoped AppDbContext for its stores. AddDbContextFactory
        // registers the context as singleton-options; we add a scoped AppDbContext from the same
        // factory so Identity gets its own per-request instance without a second options config.
        var connectionString = config.GetConnectionString("DefaultConnection");
        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<AppDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // TMDB typed client.
        services.AddHttpClient<ITmdbClient, TmdbClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TmdbOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
        });

        // Application services.
        // Repositories are transient: each owns a context from the factory, so concurrent
        // components never share one and dispose it when they go out of scope.
        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
        services.AddTransient<IMediaRequestRepository, MediaRequestRepository>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IProfilePictureStore, FileSystemProfilePictureStore>();
        services.AddSingleton<IEmbedUrlBuilder, EmbedUrlBuilder>();

        return services;
    }
}
