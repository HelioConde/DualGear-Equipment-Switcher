# DualGear — Equipment Switcher

An independent **Risk of Rain 2** gameplay mod developed by AlchemyFlame (Hélio Conde), based on the automatic post-activation equipment swapping mechanism built in PlayWithEliteItem in 2024.

**Status: prototype v0.1.0, not validated in game.**

## Core idea

Each player has an active equipment and a second one held in a virtual reserve. After the active equipment successfully fires, DualGear waits **0.3 seconds** and exchanges both equipment states. The reserve belongs to that player and is stored on the server, not inside MUL-T's physical extra equipment slot.

## Current development scope

- Server-only inventory mutations and per-player reserves.
- Intercepts `EquipmentSlot.PerformEquipmentAction` and swaps only after success.
- Configurable test equipment in the reserve. Default: `DroneBackup`; change `StartingReserveEquipment` in BepInEx config, or set empty to disable test seeding.
- Preserves `EquipmentState` rather than merely swapping indices, so charges/cooldown can be retained.
- Refuses swapping if an equipment pickup changes the active slot during the delay.
- Prevents simultaneous swaps for the same player.

**World pickups, extra HUD, remote-client UI, and compatibility with MUL-T / Functional Coupler are not implemented/tested yet.** Current game updates changed the equipment location API; this build must be checked before public release.

## Config

`BepInEx/config/AlchemyFlame.DualGear.cfg`: `Enabled`, `StartingReserveEquipment`, `DelaySeconds`, `VerboseLogging`.

## Build

```sh
dotnet restore DualGear/DualGear.csproj
dotnet build DualGear/DualGear.csproj -c Release --no-restore
dotnet run --project tests/DualGear.Tests/DualGear.Tests.csproj -c Release
```

GitHub Actions compiles the plugin and runs the policy tests; passing CI does not mean gameplay was verified. Download the compiled DLL from a successful workflow artifact, extract it and place it inside `BepInEx/plugins/` in a dedicated mod-testing profile.

## Roadmap

1. Compile against current Risk of Rain 2 API and validate solo prototype.
2. Capture a **second naturally acquired equipment** instead of relying on an artificial starter.
3. Add optional manual switching and a two-equipment HUD.
4. Test server/client synchronization, MUL-T and Functional Coupler.
5. Package a separately branded Thunderstore release after real gameplay testing.

## Origin

This is independent of [PlayWithEliteItem](https://github.com/HelioConde/PlayWithEliteItem). Similar equipment carrying mechanics exist in MUL-T and Functional Coupler; DualGear focuses on **automatic rotation after a successful equipment use**.

MIT licensed — Copyright © Hélio Conde (AlchemyFlame).
