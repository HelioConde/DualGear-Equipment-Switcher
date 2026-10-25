#!/usr/bin/env python3
"""Prevent accidentally adding a client-side dependency to DualGear.

This is a static guardrail, NOT a substitute for multiplayer testing.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sources = sorted((ROOT / "DualGear").glob("*.cs"))
assert sources, "No plugin C# files found"

forbidden = {
    r"\bRoR2\.UI\b": "custom RoR2 UI",
    r"\bUnityEngine\.UI\b": "Unity UI",
    r"\bUnityEngine\.UIElements\b": "UI Toolkit",
    r"\bUnityEngine\.Input\b": "client input",
    r"\bInput\.(GetKey|GetButton|GetAxis|GetMouseButton)": "client input polling",
    r"\bOnGUI\s*\(": "custom GUI rendering",
    r"\bGUI(Layout)?\.": "custom GUI rendering",
    r"\bClientRpc\b": "custom client RPC",
    r"\bTargetRpc\b": "custom target RPC",
    r"\bNetworkWriter\b": "custom network protocol",
    r"\bNetworkReader\b": "custom network protocol",
    r"\bHUD\b": "custom HUD identifiers",
}

errors = []
for source in sources:
    content = source.read_text(encoding="utf-8")
    # Source comments document the forbidden features; inspect executable lines only.
    lines = [
        line for line in content.splitlines()
        if not line.strip().startswith("//")
    ]
    joined = "\n".join(lines)
    for pattern, reason in forbidden.items():
        if re.search(pattern, joined):
            errors.append(f"{source.relative_to(ROOT)}: {reason}")

main = (ROOT / "DualGear" / "DualGear.cs").read_text(encoding="utf-8")
if main.count("NetworkServer.active") < 3:
    errors.append("Expected server authority checks before inventory changes")
for required in (
    "On.RoR2.GenericPickupController.AttemptGrant",
    "On.RoR2.EquipmentSlot.PerformEquipmentAction",
    "Inventory",
):
    if required not in main:
        errors.append("Missing required gameplay integration: " + required)

if errors:
    print("FAILED server-side contract:\n" + "\n".join(errors), file=sys.stderr)
    sys.exit(1)

print(f"PASS server-side contract: {len(sources)} C# files; no client HUD/input/RPC dependencies.")
