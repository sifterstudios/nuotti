# --- Build stage ---
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies
COPY package.json package-lock.json* pnpm-lock.yaml* bun.lockb* ./
RUN if [ -f package-lock.json ]; then npm ci --no-audit --no-fund; \ 
    elif [ -f pnpm-lock.yaml ]; then npm i -g pnpm && pnpm i --frozen-lockfile; \ 
    elif [ -f bun.lockb ]; then npm i -g bun && bun install; \ 
    else npm i --no-audit --no-fund; fi

# Copy sources and build
COPY . .
# PUBLIC_API_BASE can be passed at build time (docker build --build-arg PUBLIC_API_BASE=...)
ARG PUBLIC_API_BASE=""
ENV PUBLIC_API_BASE=$PUBLIC_API_BASE
RUN npm run build

# --- Runtime stage ---
FROM nginx:alpine AS runtime
COPY --from=build /app/build /usr/share/nginx/html
# Basic healthcheck endpoint and SPA routing
RUN printf 'server {\n  listen 80;\n  root /usr/share/nginx/html;\n  index index.html;\n  location /health { return 200 "ok"; add_header Content-Type text/plain; }\n  location / { try_files $uri $uri/ /index.html =404; }\n}\n' > /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=15s --timeout=3s --retries=10 CMD wget -qO- http://localhost/health || exit 1

# End