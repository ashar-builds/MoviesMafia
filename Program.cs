using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MoviesMafia.Components;
using MoviesMafia.Data;
using MoviesMafia.Data.Seed;
using MoviesMafia.Domain.Entities;
using MoviesMafia.Endpoints;
using MoviesMafia.Services;
using MoviesMafia.Services.Storage;
using ReactiveBlazor;

var builder = WebApplication.CreateBuilder(args);

// Application services: options, EF Core, TMDB client, repositories, email, storage, embed builder.
builder.Services.AddMoviesMafiaServices(builder.Configuration);

// ASP.NET Identity with cookie authentication (Static SSR — sign-in/out happen via endpoints).
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/access-denied";
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Blazor Static SSR (no interactive render modes — interactivity is provided by ReactiveBlazor).
builder.Services.AddRazorComponents();

// ReactiveBlazor encrypts component state with ASP.NET Data Protection.
builder.Services.AddDataProtection();
builder.Services.AddReactiveComponents(assemblies: typeof(Program).Assembly);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// Serve user avatars from the configured storage folder (kept outside wwwroot).
var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
var avatarsRoot = Path.IsPathRooted(storage.AvatarsPath)
    ? storage.AvatarsPath
    : Path.Combine(app.Environment.ContentRootPath, storage.AvatarsPath);
Directory.CreateDirectory(avatarsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(avatarsRoot),
    RequestPath = storage.AvatarsRequestPath,
});

app.MapReactiveComponents();
app.MapRazorComponents<App>();
app.MapAccountEndpoints();

await IdentitySeeder.MigrateAndSeedAsync(app.Services);

app.Run();
