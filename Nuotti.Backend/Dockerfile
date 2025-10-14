# Nuotti.Backend - ASP.NET Core API (net9.0)
# Multi-stage build: restore/build/publish then run on ASP.NET runtime

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers for better caching
COPY Nuotti.Backend/Nuotti.Backend.csproj Nuotti.Backend/
COPY Nuotti.Contracts/Nuotti.Contracts.csproj Nuotti.Contracts/
RUN dotnet restore Nuotti.Backend/Nuotti.Backend.csproj

# copy everything and publish (allow restore to ensure latest packages)
COPY . .
RUN dotnet publish Nuotti.Backend/Nuotti.Backend.csproj -c Release -o /app/publish

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# Allows dotnet to run in containers with limited entropy sources
ENV DOTNET_RUNNING_IN_CONTAINER=true
# The listening URL is provided by compose via ASPNETCORE_URLS
COPY --from=build /app/publish .

# If you prefer to lock down the user, uncomment the following line on hosts that support it
# USER $APP_UID

# Default to running the app
ENTRYPOINT ["dotnet", "Nuotti.Backend.dll"]
