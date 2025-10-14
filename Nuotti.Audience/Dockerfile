# Nuotti.Audience - Blazor WebAssembly static site

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Nuotti.Audience/Nuotti.Audience.csproj Nuotti.Audience/
COPY Nuotti.Contracts/Nuotti.Contracts.csproj Nuotti.Contracts/
RUN dotnet restore Nuotti.Audience/Nuotti.Audience.csproj

COPY . .
# Allow publish to restore to ensure latest transitive packages/analyzers are available
RUN dotnet publish Nuotti.Audience/Nuotti.Audience.csproj -c Release -o /out

# --- Runtime stage ---
FROM nginx:alpine AS runtime
COPY --from=build /out/wwwroot /usr/share/nginx/html
RUN printf 'server {\n  listen 80;\n  location /health { return 200 "ok"; }\n  location / { try_files $uri $uri/ /index.html; }\n}\n' > /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=15s --timeout=3s --retries=10 CMD wget -qO- http://localhost/health || exit 1

