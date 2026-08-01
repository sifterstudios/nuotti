<div id="top"></div>

<!-- PROJECT SHIELDS -->
<!-- You can replace or remove any badge below as you see fit -->
[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![License][license-shield]][license-url]
[![LinkedIn][linkedin-shield]][linkedin-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <!-- Replace with your logo (optional). You can keep the text title if no logo yet. -->
  <!-- <a href="https://github.com/sifterstudios/nuotti">
    <img src="docs/images/logo.png" alt="Logo" width="120" height="120">
  </a> -->

  <h3 align="center">Nuotti</h3>

  <p align="center">
    A modular, real‑time quiz and show platform built on .NET 10, Blazor, and Svelte.
    <br />
    <a href="#about-the-project"><strong>Explore the docs »</strong></a>
    <br />
    <br />
    <a href="#getting-started">Get Started</a>
    ·
    <a href="https://github.com/sifterstudios/nuotti/issues">Report Bug</a>
    ·
    <a href="https://github.com/sifterstudios/nuotti/issues">Request Feature</a>
  </p>
</div>

<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
        <li><a href="#solution-structure">Solution Structure</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#prerequisites">Prerequisites</a></li>
        <li><a href="#installation">Installation</a></li>
        <li><a href="#running-the-stack">Running the Stack</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#acknowledgments">Acknowledgments</a></li>
  </ol>
</details>

<!-- ABOUT THE PROJECT -->
## About The Project

Nuotti is a multi‑project .NET solution for running interactive quizzes and live shows. It features a backend with real‑time messaging, an audience web app, a projector display app, an audio engine, and a set of shared contracts. The system is designed around events and reducers, with testing coverage for contracts and backend behavior.

Key capabilities suggested by the codebase:
- Real‑time communication via SignalR hubs (e.g., QuizHub).
- Session management and an event bus, with an in‑memory implementation for local development.
- A Blazor WebAssembly audience app (built with MudBlazor).
- A projector/host display app (Avalonia desktop).
- A CLI simulation kit for driving scripted interactions against the backend.

This repository targets .NET 10 (the SDK version is pinned in `global.json`) and uses modern
ASP.NET Core features.

### Built With

- .NET 10 SDK (release candidate — pinned in `global.json`)
- ASP.NET Core / SignalR
- Blazor WebAssembly + MudBlazor (Audience)
- SvelteKit (the static `web` app)
- Avalonia (Projector desktop app)
- .NET Aspire (one‑command local orchestration)
- C# 14

### Solution Structure

Top‑level notable projects and folders:
- Nuotti — .NET Aspire AppHost that orchestrates the full local stack (backend, clients, and backing services).
- Nuotti.Backend — ASP.NET Core backend with endpoints, sessions, eventing, rate limiting, and hub broadcasting.
- Nuotti.Audience — Blazor WebAssembly UI for participants.
- Nuotti.Projector — Avalonia display surface for the show/host/projector.
- Nuotti.AudioEngine — Audio playback/engine components.
- Nuotti.Contracts — Shared messages, models, events, reducers, and web shared types.
- Nuotti.SimKit — CLI simulator (`nuotti-sim`) for scripted interactions against the backend.
- ServiceDefaults — Shared Aspire service configuration (telemetry, health checks, resilience).
- web — SvelteKit static web app.
- tests — Cross‑cutting unit, integration, and end‑to‑end test projects.
- Nuotti.*.Tests — Per‑project test suites (contracts, backend, projector, and more).
- docs — Documentation and design notes.

<p align="right">(<a href="#top">back to top</a>)</p>

<!-- GETTING STARTED -->
## Getting Started

Follow these instructions to set up a local development environment.

### Prerequisites

- .NET SDK 10 (the exact release‑candidate version is pinned in `global.json`)
  - Verify: `dotnet --version` should print the pinned 10.x version
- Node.js 20+ (only if you plan to work on the `web` SvelteKit app)
- Docker (only if you want the one‑command Aspire stack, which starts Postgres, Redis, and an Azure Storage emulator)

### Installation

1. Clone the repo
   - `git clone https://github.com/sifterstudios/nuotti.git`
   - `cd nuotti`
2. Ensure the correct .NET SDK is used
   - This repo provides a `global.json`; `dotnet --info` should list the pinned .NET 10 SDK.
3. Restore and build
   - `dotnet restore`
   - `dotnet build Nuotti.sln -c Debug`

### Running the Stack

The quickest way to start everything at once is the .NET Aspire AppHost (requires Docker, since it
also starts Postgres, Redis, and an Azure Storage emulator):

- One‑command local stack:
  - `dotnet run --project Nuotti`
  - Open the Aspire dashboard URL printed in the console to reach each service.

Alternatively, run projects individually in separate terminals:

- Backend (API + SignalR hub):
  - `dotnet run --project Nuotti.Backend`
  - By default this listens on http://localhost:5240. Run standalone, it uses in‑memory stores, so no database or Docker is required.

- Web (SvelteKit static app):
  - `cd web && npm install && npm run dev`
  - Navigate to the URL shown in the console (typically http://localhost:5173).

- Audience (Blazor WebAssembly app):
  - `dotnet run --project Nuotti.Audience`
  - Navigate to the URL shown in the console.

- Performer (Blazor Server app):
  - `dotnet run --project Nuotti.Performer`
  - Then open the shown URL and enter your Backend URL (e.g., http://localhost:5240) and Session code (or create a new session).
  - The Performer UI was scaffolded following patterns from HAVIT's NewProjectTemplate-Blazor:
    - Repo: https://github.com/havit/NewProjectTemplate-Blazor
    - Docs: https://github.com/havit/NewProjectTemplate-Blazor/blob/master/doc/README.md

- Projector:
  - `dotnet run --project Nuotti.Projector`

- Audio Engine:
  - `dotnet run --project Nuotti.AudioEngine`

- Simulator (CLI):
  - `dotnet run --project Nuotti.SimKit -- run --backend http://localhost:5240 --session dev`

Tip: Use your IDE run configurations (e.g., JetBrains Rider) to start multiple projects.

<p align="right">(<a href="#top">back to top</a>)</p>

## Usage

- Start Backend and Audience.
- Create or join a session from the Audience app (session code such as "dev").
- Use the Projector to display the show view.
- The Simulator (`nuotti-sim`) can drive scripted interactions against a running backend, with `baseline`, `load`, and `chaos` presets. Run it with `--help` to see all options.

Developers can explore the eventing model in `Nuotti.Backend/Eventing` and shared contracts in `Nuotti.Contracts/V1` to understand how state changes flow through the system.

<p align="right">(<a href="#top">back to top</a>)</p>

## Deployment

Nuotti can be deployed to production using Docker Compose with pre-built images from GitHub Container Registry.

### Deployment Guide

See [deploy/README.md](deploy/README.md) for deployment documentation, including:
- Unraid setup with Docker Compose
- Configuration options
- Reverse proxy setup
- Monitoring and troubleshooting

The Unraid UI walkthrough lives in [deploy/UNRAID-UI-GUIDE.txt](deploy/UNRAID-UI-GUIDE.txt).

### CI/CD

GitHub Actions automatically builds and publishes Docker images to GHCR on every push to `main`:
- `ghcr.io/sifterstudios/nuotti-backend:latest`
- `ghcr.io/sifterstudios/nuotti-audience:latest`
- `ghcr.io/sifterstudios/nuotti-web:latest`

Images are built for `linux/amd64` to match the self‑hosted Unraid runner.

<p align="right">(<a href="#top">back to top</a>)</p>

## Roadmap

- [x] Add Docker compose for one‑command startup.
- [x] CI/CD pipeline (build, test, publish artifacts).
- [x] Implement Simulator logic in Nuotti.SimKit (connect to hub, drive flows).
- [ ] Expand documentation in /docs with diagrams and message flows.
- [ ] More game modes and audience interactions.

See the open issues for a full list of proposed features and known issues.

<p align="right">(<a href="#top">back to top</a>)</p>

## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

- Fork the Project
- Create your Feature Branch (`git checkout -b feature/awesome-feature`)
- Commit your Changes (`git commit -m 'feat: add some awesome feature'`)
- Push to the Branch (`git push origin feature/awesome-feature`)
- Open a Pull Request

Please consider conventional commits for clear history. For larger changes, open an issue first to discuss what you would like to change.

<p align="right">(<a href="#top">back to top</a>)</p>

## License

This repository does not yet include a `LICENSE` file, so no license has been formally granted.
If you would like to use this project, please open an issue to discuss licensing.

<p align="right">(<a href="#top">back to top</a>)</p>

## Contact

Project Link: https://github.com/sifterstudios/nuotti

Feel free to open an issue or start a discussion.

<p align="right">(<a href="#top">back to top</a>)</p>

## Acknowledgments

- Best README Template by Othneil Drew: https://github.com/othneildrew/Best-README-Template
- .NET & ASP.NET Core teams
- JetBrains Rider for a stellar .NET IDE

<p align="right">(<a href="#top">back to top</a>)</p>

<!-- MARKDOWN LINKS & IMAGES -->
<!-- Replace these with your repository links -->
[contributors-shield]: https://img.shields.io/github/contributors/sifterstudios/nuotti.svg?style=for-the-badge
[contributors-url]: https://github.com/sifterstudios/nuotti/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/sifterstudios/nuotti.svg?style=for-the-badge
[forks-url]: https://github.com/sifterstudios/nuotti/network/members
[stars-shield]: https://img.shields.io/github/stars/sifterstudios/nuotti.svg?style=for-the-badge
[stars-url]: https://github.com/sifterstudios/nuotti/stargazers
[issues-shield]: https://img.shields.io/github/issues/sifterstudios/nuotti.svg?style=for-the-badge
[issues-url]: https://github.com/sifterstudios/nuotti/issues
[license-shield]: https://img.shields.io/github/license/sifterstudios/nuotti.svg?style=for-the-badge
[license-url]: https://github.com/sifterstudios/nuotti#license
[linkedin-shield]: https://img.shields.io/badge/LinkedIn-Connect-blue?style=for-the-badge&logo=linkedin
[linkedin-url]: https://www.linkedin.com/

## Local Docker build & run

You can test the exact same build that runs in CI on your machine.

Prereqs:
- Docker Desktop 4.x (Compose V2) or Docker Engine + docker compose plugin
- PowerShell (recommended on Windows)

Quick start (Windows):
- Build only:
  - powershell -ExecutionPolicy Bypass -File .\tools\build-local.ps1
- Build and run:
  - powershell -ExecutionPolicy Bypass -File .\tools\up-local.ps1
- Stop and clean up:
  - powershell -ExecutionPolicy Bypass -File .\tools\down-local.ps1
  - Add -Prune to also prune dangling images.

What this does:
- Uses `deploy/docker-compose.local.yml` to build and run the backend, audience, and web images locally
- Builds the images with `docker compose build --pull`, then (with `-Up`) starts them detached

Services and URLs after up:
- API:      http://localhost:5210
- Audience: http://localhost:5280
- Web:      http://localhost:5380

Troubleshooting:
- If a port is in use, change the host port mapping in `deploy/docker-compose.local.yml`
- See container logs: `docker compose -f deploy/docker-compose.local.yml logs -f`
