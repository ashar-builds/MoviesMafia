# MoviesMafia

A movie & series discovery app built on **Blazor Static SSR (.NET 10)** — and a working showcase for
[**ReactiveBlazor**](https://www.nuget.org/packages/ReactiveBlazor), which adds stateful, interactive
components to static server rendering with no SignalR circuit and no WebAssembly.

## Stack

- **Blazor Static SSR** on .NET 10 (no interactive render modes)
- **ReactiveBlazor** for in-place interactivity (search, trailers, requests, admin, profile)
- **Alpine.js** for purely client-side UI (menus, modals, hover reveals)
- **Tailwind CSS v4** (standalone CLI, no Node required) for styling
- **ASP.NET Identity** + cookie auth on **PostgreSQL** (EF Core / Npgsql)
- **TMDB** for catalog data; an auto-embed provider for playback

## Architecture

```
Components/      Razor components
  Pages/         Routable @page components (Home, Movies, Series, Search, Watch, Trailer, About)
  Account/       Login, Signup, Profile, VerifyEmail, AccessDenied
  Admin/         Role-gated admin pages
  Layout/        MainLayout, AuthLayout, NavMenu, Footer
  Reactive/      ReactiveComponent subclasses (SearchBox, TrailerPlayer, RequestMediaForm, ...)
  Shared/        Presentational components (MediaCard, RatingBadge, Pager, ...)
Endpoints/       Minimal-API account endpoints (login/logout/register/avatar — cookie-mutating)
Domain/          Entities (AppUser, MediaRequest), enums, role constants
Data/            AppDbContext, EF configurations, migrations, IdentitySeeder
Services/        TMDB client, email, avatar storage, embed builder, repositories, options, DI
Styles/          app.tailwind.css (Tailwind source)
```

Interactivity is split deliberately:
- **Enhanced forms / endpoints** for anything that mutates the auth cookie or uploads files (login, signup, logout, avatar) — these stay functional without JavaScript.
- **ReactiveBlazor** for stateful, in-place widgets (live search, trailer carousel, request form, profile password, admin user table). Cross-component refreshes use `[OnReactiveSignal<T>]`.
- **Alpine.js** for client-only visual state (mobile menu, playback-tips modal, card hover).

## Prerequisites

- .NET 10 SDK
- PostgreSQL (local or remote)
- Tailwind CSS v4 standalone CLI on your `PATH` as `tw` (or pass `-p:TailwindCli=/path/to/tailwindcss`)

## Configuration

Non-secret defaults live in `appsettings.json`. Provide secrets via **user-secrets** (dev) or environment
variables (prod). Required keys:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=movies-mafia;Username=postgres;Password=..."
dotnet user-secrets set "Tmdb:ApiKey" "<your-tmdb-api-key>"
dotnet user-secrets set "Streaming:AutoEmbedUrl" "https://<embed-provider>/embed"
dotnet user-secrets set "AdminSeed:UserName" "admin"
dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
dotnet user-secrets set "AdminSeed:Password" "<strong-password>"
# SMTP (production email confirmation):
dotnet user-secrets set "Smtp:Host" "..."  # Port/UserName/Password/FromAddress
```

In **Development**, new accounts are auto-confirmed (no SMTP needed) and the bundled
`appsettings.Development.json` has working local defaults.

## Running

```bash
# 1. Compile Tailwind once (or use --watch in a second terminal during development)
tw -i Styles/app.tailwind.css -o wwwroot/app.css --watch

# 2. Run the app (the build also compiles Tailwind via an MSBuild target)
dotnet run
```

The app applies EF migrations and seeds roles + the admin account on first start.
It listens on https://localhost:5248 by default (see `Properties/launchSettings.json`).

### Database migrations

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --output-dir Data/Migrations
```

## Production notes

- **Data Protection**: ReactiveBlazor encrypts component state with ASP.NET Data Protection. For
  multi-instance/container deployments, persist the keys (file share, database, or Redis) so encrypted
  state survives restarts and works across replicas.
- **Avatars** are stored under `Storage:AvatarsPath` (default `App_Data/avatars`) and served from
  `Storage:AvatarsRequestPath`. Mount a persistent volume in containers.
- **Trimming / AOT** are not supported (ReactiveBlazor + EF + reflection-based options). Publish normally.

## Docker

The provided `Dockerfile` (multi-stage, .NET 10) downloads the Tailwind CLI during the build so
`wwwroot/app.css` is generated at publish time. Supply secrets as environment variables at runtime.
