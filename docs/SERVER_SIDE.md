# DualGear — Server-side design contract

**Decision:** DualGear is a host-only/server-side mod. We will **not** create a custom HUD, client-side hotkeys, overlays or network messages. The design intentionally uses the normal Risk of Rain 2 equipment UI.

## Server responsibilities

1. Install DualGear only on the host/dedicated server.
2. Keep each player's second equipment **privately in memory on the server**.
3. Intercept vanilla equipment pickup on the server, preserving the previous active equipment in reserve where safe.
4. After a *successful* equipment use, swap full equipment states after a configurable delay.
5. Use vanilla `Inventory.SetEquipment` on the server. That is the only player-facing state; the game is responsible for replicating it.
6. Reset all saved reserve states when the run ends or restarts.

The plugin has no custom network packets or client UI. A connecting client should not require DualGear or its dependencies merely to display an equipment slot; the vanilla UI displays whichever equipment is currently active.

**Caution:** this is the intended design, not a confirmed compatibility guarantee. No actual game client was launched by the CI pipeline.

## Non-goals

- Custom equipment HUD (never).
- Custom hotkey/button to select equipment (not planned; automatic switch is the feature).
- Client-side BepInEx plugin installation (not intended).
- Custom RPCs, sync messages or modded equipment definitions (not intended).

## Server-only acceptance tests (must pass before release)

| Test | Acceptance condition |
| --- | --- |
| Unmodded client joins | Client connects to a modded host without installing DualGear |
| Two world pickups | After picking up A then B, B is active, A safely held server-side, no duplicate ground drop |
| After use | Only a successful use starts the configured delayed swap |
| Client inventory | Both host and vanilla client see the active equipment change |
| Cooldowns | Charge counts and cooldown state do not reset incorrectly when switching |
| Pickup race | Picking up another item while the swap is pending does not duplicate or lose equipment |
| Multiple players | Each player has an independent virtual reserve; no cross-player state |
| Stage/respawn | State persists appropriately inside the same run; run reset clears old reserves |
| DLC and other mods | No unsupported handling of extra inventory slots/sets, such as MUL-T / Functional Coupler |
| Disconnect/reconnect | The server does not leak equipment or transfer it to another user |

`tests/server_side_contract.py` prevents adding client UI, input and custom networking code by mistake. It is **static only**; gameplay/network verification is still required.
