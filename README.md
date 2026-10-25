# DualGear — Equipment Switcher

An independent **Risk of Rain 2** gameplay mod developed by AlchemyFlame (Hélio Conde), based on the automatic post-activation equipment swapping mechanism built in PlayWithEliteItem in 2024.

**Status: prototype v0.2.0, compilation and gameplay validation required.**

## Core idea

Each player has an active equipment and a second one held in a virtual reserve. After the active equipment successfully fires, DualGear waits **0.3 seconds** and exchanges both equipment states. The reserve belongs to that player and is stored on the server, not inside MUL-T's physical extra equipment slot.

## Capturing naturally acquired equipment (experimental)

With an empty reserve, when a player with one active equipment picks up another equipment in the world, DualGear temporarily clears the active location before passing the interaction to the **vanilla** pickup handler. This prevents the old equipment from being ejected; on a successful pickup it is stored as the second equipment, preserving its state. If the pickup fails, the original state is restored.

This applies **only to single-slot/single-set inventories** while no swap is pending. MUL-T and Functional Coupler use vanilla behavior until dedicated integration testing.

The default test starter is now disabled. Set `StartingReserveEquipment` to an internal equipment name to manually seed a reserve for diagnostics. No free equipment is granted by default.

## Current development scope

- Server-only inventory mutations and per-player reserves.
- Intercepts `EquipmentSlot.PerformEquipmentAction` and swaps only after success.
- Configurable test equipment in the reserve. Default: empty (no starter); change `StartingReserveEquipment` in BepInEx config, or set empty to disable test seeding.
- Preserves `EquipmentState` rather than merely swapping indices, so charges/cooldown can be retained.
- Refuses swapping if an equipment pickup changes the active slot during the delay.
- Prevents simultaneous swaps for the same player.

**World pickup capture has been implemented experimentally for ordinary inventories, but has NOT been tested in a running game.** HUD, remote-client reserve UI, MUL-T and Functional Coupler support are still pending. Current game updates changed the equipment location API; this build must be checked before public release.

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
2. Validate **second naturally acquired equipment** pickup capture (implemented experimentally).
3. Add optional manual switching and a two-equipment HUD.
4. Test server/client synchronization, MUL-T and Functional Coupler.
5. Package a separately branded Thunderstore release after real gameplay testing.

## Origin

This is independent of [PlayWithEliteItem](https://github.com/HelioConde/PlayWithEliteItem). Similar equipment carrying mechanics exist in MUL-T and Functional Coupler; DualGear focuses on **automatic rotation after a successful equipment use**.

MIT licensed — Copyright © Hélio Conde (AlchemyFlame).
