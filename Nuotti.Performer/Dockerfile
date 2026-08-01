# Nuotti.Performer - Blazor Server host (net10.0)
# Multi-stage build: restore/build/publish then run on the ASP.NET runtime.
#
# Build context is the repository root:
#   docker build -f Nuotti.Performer/Dockerfile .

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj files and restore as distinct layers for better caching
COPY Nuotti.Performer/Nuotti.Performer.csproj Nuotti.Performer/
COPY Nuotti.Contracts/Nuotti.Contracts.csproj Nuotti.Contracts/
COPY ServiceDefaults/ServiceDefaults.csproj ServiceDefaults/
RUN dotnet restore Nuotti.Performer/Nuotti.Performer.csproj

# copy everything and publish (allow restore to ensure latest packages)
COPY . .
RUN dotnet publish Nuotti.Performer/Nuotti.Performer.csproj -c Release -o /app/publish

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Allows dotnet to run in containers with limited entropy sources
ENV DOTNET_RUNNING_IN_CONTAINER=true
# curl is not present in the ASP.NET runtime image; the container HEALTHCHECK needs it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
# The listening URL is provided by compose via ASPNETCORE_URLS
COPY --from=build /app/publish .

# MapDefaultEndpoints only maps /health outside Development, so probe the Blazor host page.
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=10 \
    CMD curl -fsS http://localhost:5240/ >/dev/null || exit 1

ENTRYPOINT ["dotnet", "Nuotti.Performer.dll"]
