<div align="center">

# ⚡ DualGear — Equipment Switcher

**Two equipment choices. One active slot. Automatic rotation after use.**

A server-side-first gameplay mod for **Risk of Rain 2**, developed independently from [PlayWithEliteItem](https://github.com/HelioConde/PlayWithEliteItem).

![C#](https://img.shields.io/badge/C%23-.NET-512BD4?logo=dotnet&logoColor=white) ![Status](https://img.shields.io/badge/status-prototype%20v0.2.1-64748b) ![Architecture](https://img.shields.io/badge/architecture-server--side-2563eb)

[**How it works**](#how-it-works) · [**Build and test**](#build-and-test) · [**Server-side requirements**](docs/SERVER_SIDE.md)

</div>

> **Experimental — not a verified multiplayer release.** The project has a host/server-only design and automated checks. Actual Risk of Rain 2 gameplay with unmodded clients still needs to be tested before making compatibility guarantees.

## How it works

Each player has **one visible active equipment** and **one hidden reserve** maintained by the server.

```text
Player uses active equipment
            │
            ▼
Did the equipment action succeed?
            │ yes
            ▼
   Wait 0.3 seconds (default)
            │
            ▼
  Swap active ↔ reserve states
            │
            ▼
Vanilla inventory replicates the active item
```

The reserve is **not** a second physical equipment slot like MUL-T's; it belongs to the server's per-player state.

## Core design decisions

| Requirement | What it means |
| --- | --- |
| **Host/server installation only** | No mod installation is intended for players connecting with the original client |
| **No custom HUD** | The default equipment icon, controls and cooldown UI remain in use |
| **No client changes** | No overlays, keybinds, client plugin, RPC or custom networked content |
| **Swap after success** | No rotation after a failed equipment activation |
| **Preserve equipment state** | The swap works with `EquipmentState` to retain relevant charge/cooldown information |
| **No free starter by default** | Reserve begins empty unless diagnostic seeding is configured |

**Important distinction:** these are the implemented design choices and intended compatibility properties. Network synchronization with **unmodded clients** is not certified until verified inside the game.

### Acquiring the reserve (experimental)

For ordinary, single-slot inventories, the mod attempts to capture a **second equipment picked up in the world** instead of letting the original equipment be ejected. If the pickup fails, it restores the previous state.

This mechanism is **implemented but unverified in live gameplay**. MUL-T and Functional Coupler use their standard pickup behavior until specific compatibility tests are completed.

## Build and test

Prerequisites: .NET SDK compatible with the project and Risk of Rain 2/BepInEx dependencies resolved as configured.

```bash
git clone https://github.com/HelioConde/DualGear-Equipment-Switcher.git
cd DualGear-Equipment-Switcher

dotnet restore DualGear/DualGear.csproj
dotnet build DualGear/DualGear.csproj -c Release --no-restore
dotnet run --project tests/DualGear.Tests/DualGear.Tests.csproj -c Release
```

A successful [GitHub Actions](https://github.com/HelioConde/DualGear-Equipment-Switcher/actions) workflow can provide the compiled DLL as an artifact. Use a dedicated **BepInEx test profile** and install the compiled plugin **on the host/server only**.

**Running these tests does not run Risk of Rain 2.** A real solo test and vanilla-client multiplayer test are separate release gates.

## Configuration

When generated, the configuration path is:

`BepInEx/config/AlchemyFlame.DualGear.cfg`

| Setting | Purpose |
| --- | --- |
| `Enabled` | Enable/disable the mod |
| `StartingReserveEquipment` | Optional internal equipment name for diagnostics; empty by default |
| `DelaySeconds` | Wait after successful equipment use (default **0.3s**) |
| `VerboseLogging` | Additional diagnostic information |

## Test checklist before publication

- [ ] Equip two items naturally and confirm neither is lost unexpectedly.
- [ ] Verify automatic swap only after an equipment action actually succeeds.
- [ ] Check that charges, cooldowns and equipment state survive the swap.
- [ ] Connect clients with **no DualGear installed**, verify inventories, icon and activation.
- [ ] Test joining late, reconnecting, respawns and stage transitions.
- [ ] Test failure paths and repeated activations without duplicated swaps.
- [ ] Check MUL-T and Functional Coupler with appropriate exclusions.
- [ ] Revalidate the plugin against the current game equipment API.

**Read the [server-side architecture and test plan](docs/SERVER_SIDE.md)** for the more detailed constraints.

## Relationship to PlayWithEliteItem

[**PlayWithEliteItem**](https://github.com/HelioConde/PlayWithEliteItem) is a separately published mod that distributes random elite aspects. **DualGear** is an independent experiment in **automatic equipment rotation after activation**, inspired by earlier item-swapping work. The two mods have distinct behaviors and release lifecycles.

## Project status and license

**Prototype v0.2.1** · automated builds and policy tests · **manual gameplay and multiplayer validation pending**.

MIT licensed. Created by [Hélio Conde / AlchemyFlame](https://github.com/HelioConde). This is an independent, unofficial Risk of Rain 2 community project.
