<div id="top"></div>

<!-- PROJECT SHIELDS -->
<!-- You can replace or remove any badge below as you see fit -->
[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]
[![LinkedIn][linkedin-shield]][linkedin-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <!-- Replace with your logo (optional). You can keep the text title if no logo yet. -->
  <!-- <a href="https://github.com/your-org/nuotti">
    <img src="docs/images/logo.png" alt="Logo" width="120" height="120">
  </a> -->

  <h3 align="center">Nuotti</h3>

  <p align="center">
    A modular, real‑time quiz and show platform built on .NET 9.
    <br />
    <a href="#about-the-project"><strong>Explore the docs »</strong></a>
    <br />
    <br />
    <a href="#getting-started">Get Started</a>
    ·
    <a href="https://github.com/your-org/nuotti/issues">Report Bug</a>
    ·
    <a href="https://github.com/your-org/nuotti/issues">Request Feature</a>
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
- Session management and event bus with in‑memory implementation.
- Blazor frontend for the audience experience.
- A projector/host display app.
- Simulation kit (CLI) scaffold for automated interactions.

This repository targets .NET 9 and uses modern ASP.NET Core features.

### Built With

- .NET 9 SDK
- ASP.NET Core / SignalR
- Blazor (Server/WASM components present in Audience)
- C# 13

### Solution Structure

Top‑level notable projects and folders:
- Nuotti.Backend — ASP.NET Core backend with endpoints, sessions, eventing, rate limiting, and hub broadcasting.
- Nuotti.Audience — Blazor UI for participants.
- Nuotti.Projector — Display surface for the show/host/projector.
- Nuotti.AudioEngine — Audio playback/engine components.
- Nuotti.Contracts — Shared messages, models, events, reducers, and web shared types.
- Nuotti.SimKit — CLI simulator scaffold for scripted interactions against the backend.
- Nuotti.*.Tests — Test projects for contracts and backend.
- docs — Documentation and assets (you can add diagrams/logos here).

A handy CLI lives in src/Nuotti.SimKit (duplicated project layout); it currently contains a scaffolded Program.cs.

<p align="right">(<a href="#top">back to top</a>)</p>

<!-- GETTING STARTED -->
## Getting Started

Follow these instructions to set up a local development environment.

### Prerequisites

- .NET SDK 9.0 or later
  - Verify: `dotnet --version` should print 9.x
- Node.js (optional, only if you plan to adjust web assets in Audience or related tooling)

### Installation

1. Clone the repo
   - `git clone https://github.com/your-org/nuotti.git`
   - `cd nuotti`
2. Ensure the correct .NET SDK is used
   - This repo provides a global.json; `dotnet --info` should list .NET 9 SDK.
3. Restore and build
   - `dotnet restore`
   - `dotnet build Nuotti.sln -c Debug`

### Running the Stack

You can run projects individually in separate terminals.

- Backend (API + SignalR hub):
  - `dotnet run --project Nuotti.Backend`
  - By default this listens on http://localhost:5xxx (see console output).

- Audience (Blazor app):
  - `dotnet run --project Nuotti.Audience`
  - Navigate to the URL shown in the console (typically http://localhost:5xxx).

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

- Simulator (CLI scaffold):
  - `dotnet run --project src/Nuotti.SimKit -- run --backend http://localhost:5240 --session dev`

Tip: Use your IDE run configurations (e.g., JetBrains Rider) to start multiple projects.

<p align="right">(<a href="#top">back to top</a>)</p>

## Usage

- Start Backend and Audience.
- Create or join a session from the Audience app (session code such as "dev").
- Use the Projector to display the show view.
- The Simulator can be used to script interactions once its logic is implemented (the current scaffold validates arguments and prints help).

Developers can explore the eventing model in `Nuotti.Backend/Eventing` and shared contracts in `Nuotti.Contracts/V1` to understand how state changes flow through the system.

<p align="right">(<a href="#top">back to top</a>)</p>

## Roadmap

- [ ] Implement full Simulator logic in Nuotti.SimKit (connect to hub, drive flows).
- [ ] Expand documentation in /docs with diagrams and message flows.
- [ ] Add Docker compose for one‑command startup.
- [ ] CI/CD pipeline (build, test, publish artifacts).
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

Distributed under the MIT License. See `LICENSE` for more information.

<p align="right">(<a href="#top">back to top</a>)</p>

## Contact

Project Link: https://github.com/your-org/nuotti

Feel free to open an issue or start a discussion.

<p align="right">(<a href="#top">back to top</a>)</p>

## Acknowledgments

- Best README Template by Othneil Drew: https://github.com/othneildrew/Best-README-Template
- .NET & ASP.NET Core teams
- JetBrains Rider for a stellar .NET IDE

<p align="right">(<a href="#top">back to top</a>)</p>

<!-- MARKDOWN LINKS & IMAGES -->
<!-- Replace these with your repository links -->
[contributors-shield]: https://img.shields.io/github/contributors/your-org/nuotti.svg?style=for-the-badge
[contributors-url]: https://github.com/your-org/nuotti/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/your-org/nuotti.svg?style=for-the-badge
[forks-url]: https://github.com/your-org/nuotti/network/members
[stars-shield]: https://img.shields.io/github/stars/your-org/nuotti.svg?style=for-the-badge
[stars-url]: https://github.com/your-org/nuotti/stargazers
[issues-shield]: https://img.shields.io/github/issues/your-org/nuotti.svg?style=for-the-badge
[issues-url]: https://github.com/your-org/nuotti/issues
[license-shield]: https://img.shields.io/github/license/your-org/nuotti.svg?style=for-the-badge
[license-url]: https://github.com/your-org/nuotti/blob/main/LICENSE
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
- Uses deploy/docker-compose.yml (same as CI) plus deploy/docker-compose.override.yml (local-only overrides)
- Local override swaps env_file paths to in-repo .env.example files so you don’t need Unraid paths
- For the Web app build, PUBLIC_API_BASE defaults to http://localhost:5210; override by setting an env var PUBLIC_API_BASE or editing deploy/docker-compose.override.yml

Services and URLs after up:
- API:      http://localhost:5210
- Audience: http://localhost:5280
- Web:      http://localhost:5380

Troubleshooting:
- If a port is in use, change the host port mapping in deploy/docker-compose.override.yml
- If you want to use real secrets, copy each .env.example to .env and point env_file there in the override
- See container logs: docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.override.yml logs -f
