# Open Empires

An Age of Empires-inspired RTS built with Unity 6 and a Rust relay server backend, featuring deterministic lockstep multiplayer.

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

A pre-built WebGL version is available in the `docs/` folder and can be served as a static site.

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
docs/                  # WebGL build
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

For third-party license information, see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
