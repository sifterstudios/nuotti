---
name: project_vision
description: What Nuotti is, its deployment model, and the goal
type: project
---

Nuotti is a live music-guessing show platform (Kahoot meets Bandle) intended as a monetizable hosted SaaS.

**Deployment model (hybrid):**
- Central SaaS: Backend, Audience web app, Performer app — hosted centrally
- Local (band's computer): Projector (Avalonia) + AudioEngine — runs on-premises at the event venue
- The Projector/local machine is the source of truth for the song library and audio files
- The central SaaS only needs song IDs/metadata for game logic; actual audio playback is local
- This avoids streaming audio through the cloud (latency, cost) while keeping audience connectivity central

**Content source:**
- Songs/audio files live on the Projector's local machine (to be tackled later)
- Some form of song IDs will be registered at the central SaaS to link game sessions to audio content
- ManifestService and AudioStorage in Performer are related to this

**Goals:**
- Polished, monetizable product
- Built properly: good architecture, test coverage, clean patterns
- Learning project — wants to do it right, not just fast

**SimKit:**
- Simulation kit for scripting game scenarios and verifying business logic behavior
- Used to walk through phase transitions, scoring, edge cases without a live session

**Why:** This shapes priority decisions — polish and correctness matter as much as features.
**How to apply:** Favor architecture that supports multi-tenancy and clean SaaS boundaries. Projector/AudioEngine are always local; don't design them for cloud deployment.
