# Nuotti Deployment

Simple deployment instructions for local development and Unraid production.

## 🏠 Unraid Deployment (Cloudflare Tunnel)

### What You Need
1. Cloudflare Tunnel pointing to your Unraid server
2. Three domains configured (see below)

### Setup in Unraid Docker Compose UI

**1. Paste this docker-compose.yml:**

```yaml
networks:
  nuotti:
    name: nuotti

services:
  api:
    image: ghcr.io/sifterstudios/nuotti-backend:latest
    container_name: nuotti-api
    restart: unless-stopped
    
    environment:
      # IMPORTANT: Replace with YOUR actual domains!
      - NUOTTI_AllowedOrigins=https://api.nuotti.app,https://audience.nuotti.app,https://nuotti.app
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5210
    
    ports:
      - "5210:5210"
    networks:
      - nuotti

  audience:
    image: ghcr.io/sifterstudios/nuotti-audience:latest
    container_name: nuotti-audience
    restart: unless-stopped
    
    volumes:
      - /mnt/user/appdata/nuotti/audience-appsettings.json:/usr/share/nginx/html/appsettings.json:ro
    
    ports:
      - "5280:80"
    networks:
      - nuotti

  web:
    image: ghcr.io/sifterstudios/nuotti-web:latest
    container_name: nuotti-web
    restart: unless-stopped
    
    ports:
      - "5380:80"
    networks:
      - nuotti

# OPTIONAL: Auto-updates (uncomment to enable)
#  watchtower:
#    image: containrrr/watchtower
#    container_name: nuotti-watchtower
#    restart: unless-stopped
#    volumes:
#      - /var/run/docker.sock:/var/run/docker.sock
#    environment:
#      - WATCHTOWER_CLEANUP=true
#      - WATCHTOWER_POLL_INTERVAL=3600
#    command: nuotti-api nuotti-audience nuotti-web
```

**2. Create ONE file on Unraid:**

SSH into Unraid and create:
```bash
mkdir -p /mnt/user/appdata/nuotti
nano /mnt/user/appdata/nuotti/audience-appsettings.json
```

Paste this (replace with YOUR domain):
```json
{"BackendUrl":"https://api.nuotti.app"}
```

**3. Configure Cloudflare Tunnel:**

Create three public hostnames:

| Service | Subdomain | Port | WebSocket |
|---------|-----------|------|-----------|
| API | `api` | 5210 | ✅ **YES** |
| Audience | `audience` | 5280 | ❌ No |
| Web | (root) | 5380 | ❌ No |

**Important**: Enable WebSocket for the API service (SignalR needs it)!

**4. Deploy:**

Click "Compose Up" in Unraid UI!

### Update to Latest Version

In Unraid UI:
1. Click "Compose Down"
2. Click "Pull" (pulls latest images from GitHub)
3. Click "Compose Up"

### Troubleshooting

**CORS errors:**
- Make sure `NUOTTI_AllowedOrigins` in the compose file matches YOUR domains exactly
- No trailing slashes!
- Use `https://` (not `http://`)

**Audience can't connect:**
```bash
# Check if file is mounted correctly
docker exec nuotti-audience cat /usr/share/nginx/html/appsettings.json
# Should show: {"BackendUrl":"https://api.nuotti.app"}
```

**View logs:**
```bash
docker logs nuotti-api
docker logs nuotti-audience
docker logs nuotti-web
```

---

## 💻 Local Development (Windows/Mac/Linux)

### Start
```powershell
# Windows
.\tools\up-local.ps1
```

```bash
# Linux/Mac
docker compose -f deploy/docker-compose.local.yml up -d
```

### Access
- API: http://localhost:5210
- API Health: http://localhost:5210/health/ready
- Audience: http://localhost:5280
- Web: http://localhost:5380

### Stop
```powershell
# Windows
.\tools\down-local.ps1
```

```bash
# Linux/Mac  
docker compose -f deploy/docker-compose.local.yml down
```

---

## 📁 Files in This Directory

- **`docker-compose.unraid.yml`** - Production deployment (for reference, paste into Unraid UI)
- **`docker-compose.local.yml`** - Local development (used by `tools/up-local.ps1`)
- **`audience-appsettings.unraid.json`** - Template for Audience config
- **`README.md`** - This file

---

## 🔄 Auto-Updates with Watchtower

To enable automatic updates, uncomment the `watchtower` section in your docker-compose.yml and redeploy.

Watchtower will:
- Check for new images every hour
- Auto-pull and restart containers when updates are available
- Clean up old images

---

## 🎯 Quick Reference

### Unraid Commands
```bash
# View logs
docker logs nuotti-api -f

# Restart a service
docker restart nuotti-api

# Check status
docker ps | grep nuotti
```

### Images Are Built Automatically
Every push to `main` triggers GitHub Actions to build and publish:
- `ghcr.io/sifterstudios/nuotti-backend:latest`
- `ghcr.io/sifterstudios/nuotti-audience:latest`  
- `ghcr.io/sifterstudios/nuotti-web:latest`

---

## ❓ Need Help?

- **Issues**: https://github.com/sifterstudios/nuotti/issues
- **Discussions**: https://github.com/sifterstudios/nuotti/discussions
