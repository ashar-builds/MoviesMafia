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
        var connectionString = config.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        // TMDB typed client.
        services.AddHttpClient<ITmdbClient, TmdbClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TmdbOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
        });

        // Application services.
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IMediaRequestRepository, MediaRequestRepository>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IProfilePictureStore, FileSystemProfilePictureStore>();
        services.AddSingleton<IEmbedUrlBuilder, EmbedUrlBuilder>();

        return services;
    }
}
