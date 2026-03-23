<div align="center">

<img src="openempirescover.png" alt="Open Empires" width="800"/>

<br/>
<br/>

[![Discord](https://img.shields.io/badge/Discord-Join%20Community-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.com/invite/htUt9qv6Vk)
[![YouTube](https://img.shields.io/badge/YouTube-ChillyEmpire-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://www.youtube.com/@ChillyEmpire)
[![Unity](https://img.shields.io/badge/Unity%206-6000.3.9f1-000000?style=for-the-badge&logo=unity&logoColor=white)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

<br/>

**An open-source Age of Empires-inspired RTS built with Unity 6 and a Rust relay server backend, featuring deterministic lockstep multiplayer.**

[**Play the Demo**](https://chilly5.itch.io/open-empires)

</div>

---

## Features

<div align="center">

### Gather resources on a procedurally generated map

<img src="resourcegathering.gif" alt="Resource Gathering" width="700"/>

### Build a civilization based on the English, French, or Holy Roman Empire

<img src="build_a_civilization.gif" alt="Build a Civilization" width="700"/>

### Age of Empires-inspired medieval combat system

<img src="aoe_style_combat.gif" alt="Combat System" width="700"/>

### Support for multiplayer 1v1, 2v2, 3v3, and 4v4!

<img src="supportfor4v4.gif" alt="Multiplayer Support" width="700"/>

</div>

---

## Tech Stack

- **Engine**: Unity 6 (6000.3.9f1) with URP and New Input System
- **Backend**: Rust (Axum + Tokio + WebSockets), PostgreSQL for matchmaking
- **Multiplayer**: Deterministic lockstep with dynamic input delay
- **Math**: Fixed-point (Q16.16) arithmetic for cross-platform determinism

## Getting Started

### Unity Client

1. Clone the repository
2. Open the `Open Empires/` folder in Unity Hub (not the repo root)
3. Use Unity 6 (6000.3.9f1) or compatible
4. Open the main scene and press Play

### Backend Server

1. Install [Rust](https://rustup.rs/)
2. Install [PostgreSQL](https://www.postgresql.org/download/) and create a database
3. Set the `DATABASE_URL` environment variable (e.g. `postgres://user:pass@localhost/openempires`)
4. Run the server:
   ```bash
   cd backend
   cargo run
   ```
5. **Optional**: Place a [MaxMind GeoLite2 City](https://dev.maxmind.com/geoip/geolite2-free-geolocation-data) database at `data/GeoLite2-City.mmdb` for player geolocation

### WebGL Build

WebGL builds are automatically deployed to itch.io via GitHub Actions on every push to `main`.

## Architecture

### Sim/Render Separation

All gameplay state lives in pure C# simulation classes (`GameSimulation`, `SquadData`, `SoldierData`, `MapData`) with no Unity dependencies. MonoBehaviour renderers (`SquadView`, `SoldierView`, `MapRenderer`) read simulation state and update visuals each frame.

### Deterministic Lockstep

Players send commands to a relay server, which broadcasts all inputs. Clients execute the same commands in the same order using fixed-point math and a deterministic PRNG, with checksums to detect desync.

### Unit System

Individual units are controlled directly. Villagers gather resources and construct buildings, while military units (spearmen, archers, horsemen, etc.) handle combat.

### Resource Gathering

Villagers gather resources (food, wood, gold, stone) to fuel your economy and advance through ages.

## Project Structure

```
Open Empires/
  Assets/
    Scripts/
      Core/           # GameSimulation, GameBootstrapper, GameSetup
      Units/           # UnitData, movement, combat
      Network/         # CommandSerializer, relay client
    Prefabs/           # Unit and building prefabs
    Scenes/            # Game scenes
backend/               # Rust relay/matchmaking server
.github/workflows/     # CI/CD pipeline
```

## Community

- [Discord](https://discord.com/invite/htUt9qv6Vk) - Join the community, ask questions, share feedback
- [YouTube](https://www.youtube.com/@ChillyEmpire) - Devlogs and gameplay videos

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

For third-party license information, see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
