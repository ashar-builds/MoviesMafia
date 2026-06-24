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

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) (local or remote)
- [A free TMDB API key](https://www.themoviedb.org/settings/api)
- Tailwind CSS v4 [standalone CLI](https://github.com/tailwindlabs/tailwindcss/releases) on your `PATH` as `tw` (or pass `-p:TailwindCli=/path/to/tailwindcss`). No Node.js required.

## Quick start (clone & run)

No secrets are committed to this repo, so after cloning you supply your own via
[**user-secrets**](https://learn.microsoft.com/aspnet/core/security/app-secrets) (they live
outside the repo and load automatically in Development). The committed `appsettings.json` holds
only non-secret defaults.

```bash
# 1. Clone and enter the repo
git clone https://github.com/<you>/MoviesMafia.git
cd MoviesMafia

# 2. Create the database (any PostgreSQL instance works; adjust the name/credentials to taste)
createdb movies-mafia        # or: psql -U postgres -c "CREATE DATABASE \"movies-mafia\";"

# 3. Provide the two required secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=movies-mafia;Username=postgres;Password=postgres"
dotnet user-secrets set "Tmdb:ApiKey" "<your-tmdb-api-key>"

# 4. (Optional but recommended) seed an admin account so you can reach /admin
dotnet user-secrets set "AdminSeed:UserName" "admin"
dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
dotnet user-secrets set "AdminSeed:Password" "<strong-password>"

# 5. (Optional) playback provider for the Watch page
dotnet user-secrets set "Streaming:AutoEmbedUrl" "https://<embed-provider>"

# 6. Run — the build compiles Tailwind, then EF migrations + role/admin seeding run on first start
dotnet run
```

Then open **https://localhost:5248** (see `Properties/launchSettings.json`).

> **In Development, new accounts are auto-confirmed** — no SMTP needed to register and sign in.
> SMTP (`Smtp:Host`, `Smtp:UserName`, `Smtp:Password`, …) is only required in production, where
> email confirmation is enforced. Set those secrets too if you want to test the email flow locally.

### Tailwind during development

The build compiles `Styles/app.tailwind.css → wwwroot/app.css` via an MSBuild target, so a plain
`dotnet run` is enough. For live CSS updates while editing, run the watcher in a second terminal:

```bash
tw -i Styles/app.tailwind.css -o wwwroot/app.css --watch
```

### Full list of configuration keys

| Key | Required (dev) | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | **yes** | — |
| `Tmdb:ApiKey` | **yes** | — (validated on startup) |
| `AdminSeed:UserName` / `:Email` / `:Password` | recommended | empty → no admin seeded |
| `Streaming:AutoEmbedUrl` | optional | empty |
| `Smtp:Host` / `:Port` / `:UserName` / `:Password` / `:FromAddress` / `:FromName` / `:EnableSsl` | prod only | `587` / `no-reply@moviesmafia.local` / `MoviesMafia` / `true` |
| `Tmdb:BaseUrl` / `:ImageBaseUrl` | no | public TMDB URLs |
| `Storage:AvatarsPath` / `:AvatarsRequestPath` | no | `App_Data/avatars` / `/avatars` |

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

## Deployment (GitHub Actions → MonsterASP)

`.github/workflows/deploy.yml` builds, publishes, and deploys over SFTP to MonsterASP on every push
to `master` (or via **Run workflow**). It writes secrets into `appsettings.Production.json` at deploy
time — nothing sensitive is committed. Configure these under **Settings → Secrets and variables →
Actions** in your GitHub repo:

**Required** (the workflow fails fast, listing any that are missing):

| Secret | Purpose |
|---|---|
| `MONSTERASP_FTP_SERVER` | SFTP host |
| `MONSTERASP_FTP_USERNAME` | SFTP username |
| `MONSTERASP_FTP_PASSWORD` | SFTP password |
| `DEFAULT_CONNECTION_STRING` | Production PostgreSQL connection string |
| `TMDB_API_KEY` | TMDB API key (app won't start without it) |
| `ADMIN_USERNAME` | Seeded admin username |
| `ADMIN_EMAIL` | Seeded admin email |
| `ADMIN_PASSWORD` | Seeded admin password |
| `SMTP_HOST` | SMTP server (prod enforces email confirmation) |
| `SMTP_USERNAME` | SMTP username |
| `SMTP_PASSWORD` | SMTP password |

**Optional** (fall back to `appsettings.json` defaults if unset):

| Secret | Default |
|---|---|
| `MONSTERASP_FTP_SERVER_DIR` | `/wwwroot` |
| `STREAMING_AUTOEMBED_URL` | empty |
| `SMTP_PORT` | `587` |
| `SMTP_FROM_ADDRESS` | `no-reply@moviesmafia.local` |
| `SMTP_FROM_NAME` | `MoviesMafia` |
| `SMTP_ENABLE_SSL` | `true` |
| `ADMIN_PROFILE_PICTURE` | empty |

> **Note:** MonsterASP must reach a PostgreSQL instance (this app uses Npgsql, not SQL Server).
> For multi-instance hosting, persist Data Protection keys (see Production notes) so encrypted
> ReactiveBlazor state survives restarts.
