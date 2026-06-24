# See https://aka.ms/customizecontainer to learn how to customize your debug container.

# Base runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage. Downloads the standalone Tailwind v4 CLI so the BuildTailwind MSBuild
# target can compile wwwroot/app.css during publish (no Node toolchain required).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TAILWIND_VERSION=v4.3.0
WORKDIR /src

RUN curl -sSL -o /usr/local/bin/tw \
        "https://github.com/tailwindlabs/tailwindcss/releases/download/${TAILWIND_VERSION}/tailwindcss-linux-x64" \
    && chmod +x /usr/local/bin/tw

COPY ["MoviesMafia.csproj", "."]
RUN dotnet restore "./MoviesMafia.csproj"
COPY . .
RUN dotnet publish "./MoviesMafia.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage.
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MoviesMafia.dll"]
