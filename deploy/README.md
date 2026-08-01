# Nuotti Deployment

## What gets deployed where

Nuotti is a hybrid: part of it is a hosted service, part of it runs at the venue.

| Component | Where it runs | Why |
|---|---|---|
| `Nuotti.Backend` | **Hosted** | Session state, SignalR fan-out, durable stores |
| `Nuotti.Audience` | **Hosted** | Static Blazor WASM the crowd loads on their phones |
| `Nuotti.Performer` | **Hosted** | Blazor Server control surface |
| `web` | **Hosted** | Marketing site |
| Postgres / Redis / Azurite | **Hosted** | Backend's database, SignalR backplane, and private-asset blob store |
| `Nuotti.Projector` | **Venue-local** | Avalonia desktop app on the band's machine |
| `Nuotti.AudioEngine` | **Venue-local** | Owns the audio files and playback hardware |

The venue-local pair is marked `ExcludeFromManifest()` in `Nuotti/Program.cs` and ships as a
separate `win-x64` package (see `docs/production-packaging.md`). Nothing in this directory
deploys them.

---

## Unraid deployment (Cloudflare Tunnel)

### 1. Create the appdata directory and the audience config

SSH into Unraid:

```bash
mkdir -p /mnt/user/appdata/nuotti/logs /mnt/user/appdata/nuotti/performer-dpkeys
cat > /mnt/user/appdata/nuotti/audience-appsettings.json <<'JSON'
{"BackendUrl":"https://api.nuotti.app"}
JSON
```

The Audience image is domain-agnostic; this file is the only thing that points it at your
backend. Replace `nuotti.app` with your own domain here and everywhere below.

### 2. Create the stack `.env`

Copy `deploy/.env.unraid.example` to a file named `.env` **next to the compose file**. In the
Unraid Compose Manager that is *Edit Stack → .env*. Compose reads `${VAR}` substitutions only
from there — an `env_file:` entry inside a service does not satisfy them.

```bash
NUOTTI_DOMAIN=nuotti.app
POSTGRES_PASSWORD=$(openssl rand -base64 24)
AZURITE_ACCOUNT=nuottiassets
AZURITE_ACCOUNT_KEY=$(openssl rand -base64 32)
```

> **Do not use Azurite's default `devstoreaccount1` account.** Its key is published in
> Microsoft's own documentation, and the blob endpoint below is exposed to the internet so
> browsers can follow the SAS URLs the Backend mints. With the default key, anyone can forge
> a grant against your storage. `AZURITE_ACCOUNTS` in the compose file replaces it.

### 3. Paste `docker-compose.unraid.yml` into the Compose Manager

Use the file in this directory verbatim. It brings up seven containers: `postgres`, `redis`,
`azurite`, `api`, `performer`, `audience`, `web`. Postgres and Redis publish no host ports and
are reachable only on the internal `nuotti` network.

### 4. Configure the Cloudflare Tunnel

Five public hostnames:

| Service | Hostname | Unraid target | WebSockets |
|---|---|---|---|
| Web | `nuotti.app` | `http://<unraid-ip>:5380` | No |
| Audience | `audience.nuotti.app` | `http://<unraid-ip>:5280` | No |
| Performer | `performer.nuotti.app` | `http://<unraid-ip>:5480` | **Yes** |
| API | `api.nuotti.app` | `http://<unraid-ip>:5210` | **Yes** |
| Assets | `assets.nuotti.app` | `http://<unraid-ip>:5100` | No |

WebSockets are mandatory on **two** hostnames, not one: `api` carries the SignalR hub, and
`performer` carries the Blazor Server circuit. Performer will render and then go inert without it.

`assets` must be public because `Assets/AzurePrivateAssetObjectStore.cs` hands SAS URLs
directly to the browser, and those URLs are built from the `BlobEndpoint` in the connection
string. Cloudflare's free plan caps request bodies at 100 MB, which is also your effective
upload cap for a private song asset.

### 5. Deploy

```bash
docker compose -f deploy/docker-compose.unraid.yml pull
docker compose -f deploy/docker-compose.unraid.yml up -d
```

Or click *Compose Up* in the Unraid UI.

### 6. Verify

```bash
docker ps --filter name=nuotti --format '{{.Names}}\t{{.Status}}'   # all should say (healthy)
curl -s https://api.nuotti.app/health/ready | jq                     # status: Healthy
docker exec nuotti-postgres psql -U nuotti -d nuotti -c '\dt'        # nuotti_* tables exist
```

The `nuotti_*` tables are created lazily by each Postgres store's `EnsureSchemaAsync`, so they
appear on first use rather than at startup. An empty `\dt` on a freshly deployed stack is
expected; it is only a problem if it stays empty after you have run a session.

Confirm the Backend actually picked up its stores — if a connection string is missing,
`Program.cs` falls back to in-memory silently and you lose everything on restart:

```bash
docker logs nuotti-api 2>&1 | grep -i "npgsql\|redis\|blob"
```

### 7. Known gap: nobody can sign in yet

`POST /v1/auth/magic-links` issues a token and then hands it to
`Workspaces/MagicLinkDelivery.cs`, which posts it to a webhook. Outside Development the
token is deliberately never returned in the HTTP response, so with no webhook configured
the endpoint logs `Magic-link delivery is not configured` and returns **503** — which means
no Workspace sign-in, which means no Performer login.

The stack is otherwise fully functional without it. When you have an email service, set
`Nuotti__MagicLinkDeliveryUrl` on the `api` service (there is a commented line in the
compose file) and restart.

---

## Updating

```bash
docker compose -f deploy/docker-compose.unraid.yml pull
docker compose -f deploy/docker-compose.unraid.yml up -d
```

In the Unraid UI: *Compose Down* → *Pull* → *Compose Up*. Images are rebuilt and pushed to
GHCR by `.github/workflows/build-and-push.yml` on every push to `main`.

That workflow runs on `[self-hosted, unraid, docker, amd64]`. If no runner with those labels
is registered, builds queue forever and `:latest` silently stays stale — check
`gh run list` and `gh api /repos/sifterstudios/nuotti/actions/runners` before assuming a
deploy is out of date for any other reason.

---

## Troubleshooting

**CORS errors / audience cannot reach the API**

The allowlist variable is `Nuotti__AllowedOrigins` with a **double underscore**.
`Program.cs` reads the config key `Nuotti:AllowedOrigins`; a `NUOTTI_`-prefixed variable has
its prefix stripped and lands on `AllowedOrigins` instead, leaving the allowlist empty — which
denies every cross-origin request. No trailing slashes, `https://` not `http://`.

```bash
docker exec nuotti-api printenv | grep -i allowedorigins
```

**Audience connects to the wrong backend**

```bash
docker exec nuotti-audience cat /usr/share/nginx/html/appsettings.json
```

**Performer loads but nothing responds** — WebSockets are off on the `performer` hostname.

**Private asset upload or download fails** — the SAS URL points somewhere the browser cannot
reach. Check that `BlobEndpoint` in the Backend's `ConnectionStrings__assets` is the public
`https://assets.<domain>/<account>` form, not `http://azurite:10000`.

**Logs**

```bash
docker logs nuotti-api -f
docker logs nuotti-performer -f
ls /mnt/user/appdata/nuotti/logs/Nuotti.Backend/   # 30-day audit trail
```

---

## Local development

Aspire is the one-command path and starts everything, including the venue-local pair:

```bash
dotnet run --project Nuotti
```

The container-only subset (api + audience + web, no Postgres/Redis/blob):

```bash
docker compose -f deploy/docker-compose.local.yml up -d --build   # Linux/Mac
.\tools\up-local.ps1                                              # Windows
```

- API: http://localhost:5210 (health: `/health/ready`)
- Audience: http://localhost:5280
- Web: http://localhost:5380

---

## Files in this directory

| File | Purpose |
|---|---|
| `docker-compose.unraid.yml` | Hosted stack for Unraid |
| `.env.unraid.example` | Template for the stack `.env` (secrets; copy to `.env`, never commit) |
| `audience-appsettings.unraid.json` | Template for the Audience `BackendUrl` file |
| `docker-compose.local.yml` | Local container subset, used by `tools/up-local.ps1` |
| `UNRAID-UI-GUIDE.txt` | Click-by-click walkthrough of the Unraid Compose Manager |
