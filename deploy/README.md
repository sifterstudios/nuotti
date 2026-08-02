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
mkdir -p /mnt/user/appdata/nuotti/{logs,performer-dpkeys,observability/grafana/provisioning/datasources}
cat > /mnt/user/appdata/nuotti/audience-appsettings.json <<'JSON'
{"BackendUrl":"https://api.nuotti.app"}
JSON
```

Copy the observability configs from this repo onto the Unraid share (required before
`loki` / `alloy` / `grafana` can start — Compose Manager pastes only the YAML, not these files):

```bash
# From a machine that has the repo checked out, or paste the file contents on Unraid:
cp deploy/observability/loki-config.yml \
  /mnt/user/appdata/nuotti/observability/loki-config.yml
cp deploy/observability/alloy-config.alloy \
  /mnt/user/appdata/nuotti/observability/alloy-config.alloy
cp deploy/observability/grafana/provisioning/datasources/loki.yml \
  /mnt/user/appdata/nuotti/observability/grafana/provisioning/datasources/loki.yml
```

Grafana / Loki / Alloy data lives in Docker named volumes (`nuotti-grafana-data`, etc.), not
under appdata, so you do not need to `chown` anything for those containers.

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
GRAFANA_ADMIN_PASSWORD=$(openssl rand -base64 24)
```

> **Do not use Azurite's default `devstoreaccount1` account.** Its key is published in
> Microsoft's own documentation, and the blob endpoint below is exposed to the internet so
> browsers can follow the SAS URLs the Backend mints. With the default key, anyone can forge
> a grant against your storage. `AZURITE_ACCOUNTS` in the compose file replaces it.

### 3. Paste `docker-compose.unraid.yml` into the Compose Manager

Use the file in this directory verbatim. It brings up ten containers: `postgres`, `redis`,
`azurite`, `api`, `performer`, `audience`, `web`, `loki`, `alloy`, `grafana`. Postgres, Redis,
Loki, and Alloy publish no host ports and are reachable only on the internal `nuotti` network.
Grafana listens on host port `5580` for LAN access only — do not add a Cloudflare Tunnel
hostname for it without SSO or another auth layer in front.

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

### 7. Sign-in and email

Workspace sign-in is a magic link: you enter an email at `https://performer.<domain>/signin`,
the Backend issues a single-use token, and delivery emails it as a link back to that same page.
Outside Development the token is never returned in the HTTP response, so **delivery must be
configured or nobody can sign in** — the endpoint returns 503.

Two delivery adapters ship, selected by config:

| Adapter | Configure | Use when |
|---|---|---|
| Mailgun | `Nuotti__Mailgun__*` (four keys) | You have a Mailgun account — this is the default path |
| Webhook | `Nuotti__MagicLinkDeliveryUrl` | You want to post the raw token to your own service |

Mailgun wins if its section is complete. A **partially** filled section fails at startup rather
than at the moment someone tries to sign in.

Two things to get right:

- **Region.** `Nuotti__Mailgun__BaseUrl` defaults to `https://api.eu.mailgun.net`. A US-region
  key sent to the EU host fails as a bare `401`, which looks identical to a wrong key.
- **`SignInUrl`.** The Backend issues a bare token and never a URL, so this is the only place
  that knows where a magic link points. It must be the public Performer address.

Check it end to end:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST https://api.<domain>/v1/auth/magic-links \
  -H 'Content-Type: application/json' -d '{"email":"you@example.com"}'
```

`202` means Mailgun accepted it. `503` means delivery is unconfigured or Mailgun rejected the
send — `docker logs nuotti-api` carries Mailgun's own reason in that case.

### 8. Observability (Grafana + Loki + Alloy)

Alloy scrapes stdout from every `nuotti-*` container (except the log stack itself) and ships
it to Loki. Grafana is the Explore UI — the Aspire-like multi-service log view for production.

1. Open `http://<unraid-ip>:5580` on your LAN.
2. Sign in as `admin` / your `GRAFANA_ADMIN_PASSWORD`.
3. **Explore** → datasource **Loki** → query e.g. `{container="nuotti-api"}`.

Useful LogQL filters:

```logql
{container="nuotti-api"}
{container=~"nuotti-(api|performer)"}
{service="api"} |= "error"
```

**Do not** expose Grafana on Cloudflare unless you add SSO or another gate in front. The
admin password alone is not enough for a public hostname.

To raise Serilog verbosity temporarily while digging in Grafana, set `NUOTTI_LOG_LEVEL=Information`
in the stack `.env` and recreate `api` (and `performer` if needed), then put it back to
`Warning`.

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

Prefer Grafana on the LAN (`http://<unraid-ip>:5580` → Explore → Loki) for multi-container
browsing. Fallback:

```bash
docker logs nuotti-api -f
docker logs nuotti-performer -f
ls /mnt/user/appdata/nuotti/logs/Nuotti.Backend/   # 30-day audit trail
```

If Grafana Explore is empty: check `docker logs nuotti-alloy` (needs the docker socket and
a healthy Loki), and confirm the config files exist under
`/mnt/user/appdata/nuotti/observability/`.

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
| `observability/` | Loki, Alloy, and Grafana provisioning configs (copy onto Unraid appdata) |
| `docker-compose.local.yml` | Local container subset, used by `tools/up-local.ps1` |
| `UNRAID-UI-GUIDE.txt` | Click-by-click walkthrough of the Unraid Compose Manager |
