# AGENTS.md — Dead Cells Multiplayer Mod

> Instructions for AI coding agents working on this project.

## Project Identity

This repository contains the multiplayer mod for Dead Cells built on top of the Dead Cells Core Modding framework (`DCCM`). It is not the core framework itself; it is a gameplay mod that plugs into the DCCM loader and synchronizes state across players.

| Property | Value |
|---|---|
| **Project** | `DeadCellsMultiplayerMod` |
| **Game** | Dead Cells |
| **Framework** | Dead Cells Core Modding (`core/`) |
| **Mod Type** | runtime multiplayer mod |
| **Target Framework** | .NET 10 |
| **Platforms** | Windows x64 |
| **License** | MIT |
| **Main Entry** | `DeadCellsMultiplayerMod.ModEntry` |

---

## Architecture Overview

### Runtime Model

The mod is loaded by the DCCM host/launcher generated from the `core/` repository. In practice, the mod is placed under the `coremod/mods` directory and is discovered by the framework through `modinfo.json` metadata and the `ModBase` lifecycle system.

The runtime flow is roughly:

```text
DeadCellsModding.exe
  → DCCM host loads core modules
  → loads user mods from coremod/mods/
  → instantiates DeadCellsMultiplayerMod.ModEntry
  → subscribes to game lifecycle events
  → creates NetNode for host/client networking
  → syncs ghost players, mobs, scene state, interaction, UI
```

### Core Design

The project is organized around a single main mod entry and several subsystem modules that each handle a specific domain:

1. **Lifecycle / bootstrap** — `ModEntry`, launch flows, setup hooks
2. **Networking** — `server/`, `SteamP2P/`, TCP/handshake logic
3. **Remote player representation** — `Ghost/`, `Kinghead`, camera/spectate logic
4. **Mob synchronization** — `Mobs/`, boss sync, combat state sync
5. **World / level sync** — `GameDataSync/`, graph reload, transitions
6. **Interaction sync** — `Interaction/`, doors, elevators, plates, markers
7. **UI / menu / lobby** — `UI/`, `Interface/`, `LaunchSync/`
8. **Support systems** — `FakeDeath/`, `AdvancedCoop/`, `PortableCore/`, `Tools/`

---

## Directory Map

### Root

| Path | Purpose |
|---|---|
| `ModEntry/` | Main mod startup and lifecycle hooks |
| `server/` | Network node, protocol, transport, packet routing |
| `Ghost/` | Remote player ghost body and render synchronization |
| `Mobs/` | Monster, boss, attack, death synchronization |
| `GameDataSync/` | Level graph / boss cell / world state sync |
| `Interaction/` | Doors, elevators, plates, pickups, state sync |
| `UI/` | In-game UI and mod HUD |
| `Interface/` | Interface modules and menu plumbing |
| `LaunchSync/` | Host/client launch coordination |
| `SteamP2P/` | Steam-specific lobby and P2P code |
| `Tools/` | Shared helpers and tooling |
| `Resourcefile/` | Localization resources and generated `.mo` files |
| `docs/` | Mod troubleshooting and documentation |
| `ProtocolReplay.Tests/` | Network/protocol regression tests |
| `PortableCore/` | Shared portable core utilities |
| `FakeDeath/` | Downed/death/revive logic |
| `GhostCine/` | Ghost-related cinematic/death visuals |
| `AdvancedCoop/` | Advanced co-op / progression support |

### Main Entry Points

- `ModEntry/ModEntry.cs` — primary mod bootstrap and lifecycle management
- `ModEntry/ModEntry.GhostSync.cs` — ghost syncing behavior
- `ModEntry/ModEntry.NetworkMenu.cs` — UI/network menu flow
- `ModEntry/ModEntry.JoinSpawn.cs` — join/spawn logic
- `ModEntry/ModEntry.ReviveDowned.cs` — downed/revive handling
- `ModEntry/ModEntry.Steam.cs` — Steam integration entry points

### Networking Core

- `server/server.cs` — `NetNode` main class and session state
- `server/server.NetNode.Protocol.*` — protocol parsing/dispatch
- `server/server.NetNode.Send*` — send paths and routing
- `server/server.NetNode.Handshake.cs` — initial handshake and connection setup
- `server/server.NetNode.RemotePlayers.cs` — remote player state cache
- `server/server.NetNode.Cleanup.cs` — disconnect/cleanup logic
- `server/server.NetNode.Steam.cs` — Steam transport logic

---

## Runtime Patterns

### Mod lifecycle

This project follows the DCCM event model: the main mod class implements lifecycle interfaces and responds to game updates in the appropriate phase.

Examples from the main entry:

```csharp
public partial class ModEntry(ModInfo info) : ModBase(info),
    IOnGameEndInit,
    IOnHeroInit,
    IOnHeroUpdate,
    IOnFrameUpdate,
    IOnAfterLoadingCDB,
    IOnAdvancedModuleInitializing
```

This means the mod is designed to react to game initialization, hero creation, per-frame updates, post-load events, and advanced module init, rather than running as a standalone process.

### Network role model

The session is centered around `NetRole`:

```csharp
public enum NetRole { None, Host, Client }
```

`NetNode` is the transport/session object. It is responsible for:

- choosing TCP vs Steam transport
- hosting or joining a lobby/session
- tracking connected clients
- sending and receiving protocol messages
- maintaining remote player state snapshots

### Ghost / remote representation

Remote players are represented as ghost-like objects rather than raw game entities. The `Ghost` subsystem creates renderable/simulatable copies of the remote hero or boss state so the host and client experience the same world view.

Key idea:

- `NetNode._remotes` = network receive buffer
- `ModEntry.clients[]` = main-thread visual projection used by rendering and ghost logic

These are intentionally different projections of the same logical player state; they serve different lifecycles and consumers.

---

## Coding Conventions

### Style

The repository follows the same broad .NET conventions as the core repo:

- C# 12 / latest project language features allowed
- primary constructors are commonly used
- `namespace` names are block scoped
- nullable reference types are enabled
- strong logging with Serilog is expected

### Naming and structure

- Types and public members use PascalCase
- Files are organized by subsystem and responsibility
- Namespaces usually mirror folder paths where practical
- Keep network/session logic and render logic separated, especially in `NetNode` and `ModEntry`

### Logging

Use structured Serilog logging rather than raw string concatenation:

```csharp
Logger.Information("[NetMod][Steam] Steam features disabled ({Reason}).", reason);
```

### Error handling

- Log failures at the point they occur
- Avoid swallowing exceptions silently
- Keep cleanup logic in session-dispose paths
- Use defensive checks around game objects and network state transitions

---

## Build and Validation

### Local build

From the project root:

```powershell
dotnet build -c Release
```

This repo is built as a mod project for the DCCM environment. The build output is usually prepared for packaging/installation in the `coremod` flow, not as a standalone executable.

### Packaging note

The project is not intended to run independently. It is meant to be packaged into the mod folder used by the DCCM runtime, and then launched via the DCCM `DeadCellsModding.exe` launcher.

---

## Module-specific guidance

### When editing networking

- Be careful with `NetNode` session lifetime and cleanup
- Check both host and client flows
- Maintain compatibility with both TCP and Steam path
- Keep packet/routing logic deterministic and session-safe

### When editing sync logic

- Prefer using existing state snapshots and caches before creating new ones
- Respect host-authoritative behavior when the design says so
- Keep state transitions predictable across join/load sequences

### When editing ghost / animation code

- Respect the separation between network receive state and rendered state
- Avoid mutating the main-thread ghost projection directly from network code
- Check whether data should be diffed or reconstructed in `ModEntry`

### When editing UI / menu logic

- Keep network-driven menu state separate from gameplay state
- Avoid hard-coding assumptions that only one player exists

---

## Working Expectations

1. Prefer surgical edits over broad rewrites.
2. Keep the architecture consistent with the DCCM event model.
3. Maintain compatibility with the `core/` framework contract.
4. When fixing multiplayer state issues, trace through the network receive path and main-thread projection path.
5. Validate with the smallest relevant build/test command after changes.

---

## Helpful entry points for future work

- `DeadCellsMultiplayerMod/ModEntry/ModEntry.cs`
- `server/server.cs`
- `Mobs/MonsterSynchronization.cs`
- `Mobs/Bosses/BossStateSync.cs`
- `GameDataSync/GameDataSync.cs`
- `Ghost/GhostHero.cs`
- `Ghost/GhostKing.cs`
- `UI/`
- `LaunchSync/`

This mod is a large event-driven multiplayer simulation layer on top of the DCCM runtime; the most important rule is to preserve the split between transport state, authoritative gameplay state, and render-state projection.
