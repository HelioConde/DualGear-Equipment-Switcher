using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DualGear
{
    // Prototype inspired by AlchemyFlame's original 2024 post-use swap technique.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DualGearPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "AlchemyFlame.DualGear";
        public const string PluginName = "DualGear - Equipment Switcher";
        public const string PluginVersion = "0.2.0";
        private const float PlayerScanInterval = 0.5f;

        private sealed class Reserve
        {
            internal EquipmentState Equipment;
            internal bool HasEquipment;
            internal bool SwapPending;
        }

        private readonly Dictionary<CharacterMaster, Reserve> reserves =
            new Dictionary<CharacterMaster, Reserve>();
        private Run trackedRun;
        private int runGeneration;
        private float nextScan;
        private ConfigEntry<bool> pluginEnabled;
        private ConfigEntry<string> startingReserve;
        private ConfigEntry<float> switchDelay;
        private ConfigEntry<bool> verboseLogging;

        private void Awake()
        {
            pluginEnabled = Config.Bind("General", "Enabled", true,
                "Enable server-authoritative automatic equipment switching.");
            startingReserve = Config.Bind("Prototype", "StartingReserveEquipment", "",
                "Internal equipment name for a test reserve. Empty disables the test starter.");
            switchDelay = Config.Bind("Switching", "DelaySeconds", 0.3f,
                new ConfigDescription("Delay after successful activation before swapping.",
                    new AcceptableValueRange<float>(0f, 2f)));
            verboseLogging = Config.Bind("Diagnostics", "VerboseLogging", false,
                "Log reserve seeding and swaps.");
            On.RoR2.EquipmentSlot.PerformEquipmentAction += OnPerformEquipmentAction;
            On.RoR2.GenericPickupController.AttemptGrant += OnAttemptGrant;
            Logger.LogInfo("DualGear v" + PluginVersion + " loaded (prototype).");
        }

        private void OnDestroy()
        {
            On.RoR2.EquipmentSlot.PerformEquipmentAction -= OnPerformEquipmentAction;
            On.RoR2.GenericPickupController.AttemptGrant -= OnAttemptGrant;
            StopAllCoroutines();
            ClearRunState();
        }

        private void Update()
        {
            if (!NetworkServer.active) return;
            Run run = Run.instance;
            if (run == null)
            {
                if (trackedRun != null) ClearRunState();
                return;
            }
            if (trackedRun != run)
            {
                ClearRunState();
                trackedRun = run;
                Logger.LogInfo("Run detected; reserves reset.");
            }
            if (!pluginEnabled.Value || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + PlayerScanInterval;
            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (player == null || player.networkUser == null || player.master == null ||
                    player.master.inventory == null || reserves.ContainsKey(player.master)) continue;

                var reserve = new Reserve();
                reserves.Add(player.master, reserve);
                SeedReserve(run, player.master, reserve);
            }
        }

        private void SeedReserve(Run run, CharacterMaster master, Reserve reserve)
        {
            string equipmentName = startingReserve.Value.Trim();
            if (equipmentName.Length == 0) return;
            EquipmentIndex index = EquipmentCatalog.FindEquipmentIndex(equipmentName);
            EquipmentDef definition = EquipmentCatalog.GetEquipmentDef(index);
            if (index == EquipmentIndex.None || definition == null ||
                (definition.requiredExpansion != null && !run.IsExpansionEnabled(definition.requiredExpansion)))
            {
                Logger.LogWarning("Test equipment unavailable: " + equipmentName);
                return;
            }
            reserve.Equipment = new EquipmentState(index, Run.FixedTimeStamp.positiveInfinity, 1, false);
            reserve.HasEquipment = true;
            if (verboseLogging.Value) Logger.LogInfo("Seeded reserve for " + master.name);
        }

        // A normal equipment pickup is the second gear source. Temporarily vacating
        // the one active location lets vanilla grant the new pickup without dropping
        // the previous equipment into the world. Store the old *full state* only
        // after checking the pickup was actually granted.
        private void OnAttemptGrant(
            On.RoR2.GenericPickupController.orig_AttemptGrant orig,
            GenericPickupController controller, CharacterBody body)
        {
            if (!NetworkServer.active || !pluginEnabled.Value || trackedRun == null ||
                controller == null || body == null || body.master == null ||
                body.inventory == null || !reserves.TryGetValue(body.master, out Reserve reserve))
            {
                orig(controller, body);
                return;
            }

            Inventory inventory = body.inventory;
            PickupDef pickup = PickupCatalog.GetPickupDef(controller.pickup.pickupIndex);
            if (pickup == null || pickup.equipmentIndex == EquipmentIndex.None)
            {
                orig(controller, body);
                return;
            }

            // Extra equipment slots/sets have their own pickup semantics.
            // Leave MUL-T and Functional Coupler to vanilla for now.
            if (!SwapPolicy.CanCapture(
                reserve.HasEquipment, reserve.SwapPending,
                inventory.equipmentSlotCount == 1 && inventory.equipmentSetCount == 1))
            {
                orig(controller, body);
                return;
            }

            EquipmentLocation location = inventory.activeEquipmentLocation;
            EquipmentState previous = inventory.GetEquipment(location);
            if (previous.equipmentIndex == EquipmentIndex.None)
            {
                orig(controller, body);
                return;
            }

            // Clear before orig so the vanilla pickup doesn't eject the original
            // equipment, then restore it if the pickup was denied.
            inventory.SetEquipment(
                new EquipmentState(EquipmentIndex.None, Run.FixedTimeStamp.positiveInfinity, 0, false),
                location.slot, location.set);

            try
            {
                orig(controller, body);
            }
            finally
            {
                EquipmentState current = inventory.GetEquipment(location);
                if (current.equipmentIndex == pickup.equipmentIndex)
                {
                    reserve.Equipment = previous;
                    reserve.HasEquipment = true;
                    if (verboseLogging.Value)
                        Logger.LogInfo("Captured previous equipment in reserve: " +
                            previous.equipmentIndex);
                }
                else if (current.equipmentIndex == EquipmentIndex.None)
                {
                    // Grant failed or was rejected: no equipment should be lost.
                    inventory.SetEquipment(previous, location.slot, location.set);
                    if (verboseLogging.Value)
                        Logger.LogInfo("Equipment grant did not complete; restored original.");
                }
                else
                {
                    // Another mod replaced the slot during the pickup. The old item
                    // was removed by us, so keep it safe instead of destroying it.
                    reserve.Equipment = previous;
                    reserve.HasEquipment = true;
                    Logger.LogWarning("Nonstandard pickup result; original saved to reserve.");
                }
            }
        }

        private bool OnPerformEquipmentAction(
            On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig,
            EquipmentSlot slot, EquipmentDef equipmentDef)
        {
            bool success = orig(slot, equipmentDef);
            if (!success || !NetworkServer.active || !pluginEnabled.Value || trackedRun == null ||
                slot == null || slot.characterBody == null) return success;

            CharacterMaster master = slot.characterBody.master;
            if (master == null || !reserves.TryGetValue(master, out Reserve reserve) ||
                !SwapPolicy.ShouldQueue(success, reserve.HasEquipment, reserve.SwapPending))
                return success;

            reserve.SwapPending = true;
            StartCoroutine(DeferredSwap(master, reserve, equipmentDef != null ? equipmentDef.equipmentIndex : EquipmentIndex.None, runGeneration));
            return success;
        }

        private IEnumerator DeferredSwap(CharacterMaster master, Reserve reserve,
            EquipmentIndex activatedIndex, int expectedGeneration)
        {
            yield return new WaitForSeconds(switchDelay.Value);
            try
            {
                if (!NetworkServer.active || !pluginEnabled.Value || trackedRun == null ||
                    runGeneration != expectedGeneration || master == null || master.inventory == null ||
                    !reserves.TryGetValue(master, out Reserve stored) ||
                    !ReferenceEquals(stored, reserve) || !reserve.HasEquipment)
                    yield break;

                Inventory inventory = master.inventory;
                EquipmentLocation location = inventory.activeEquipmentLocation;
                EquipmentState current = inventory.GetEquipment(location);
                if (!SwapPolicy.CanCommit(
                    trackedRun != null && runGeneration == expectedGeneration,
                    current.equipmentIndex == activatedIndex, reserve.HasEquipment))
                    yield break;

                EquipmentState next = reserve.Equipment;
                if (next.equipmentIndex == EquipmentIndex.None) yield break;
                inventory.SetEquipment(next, location.slot, location.set);
                if (inventory.GetEquipment(location).equipmentIndex != next.equipmentIndex)
                {
                    Logger.LogWarning("Swap did not commit; old reserve retained.");
                    yield break;
                }
                reserve.Equipment = current;
                reserve.HasEquipment = current.equipmentIndex != EquipmentIndex.None;
                if (verboseLogging.Value)
                    Logger.LogInfo("Swapped " + current.equipmentIndex + " -> " + next.equipmentIndex);
            }
            finally
            {
                reserve.SwapPending = false;
            }
        }

        private void ClearRunState()
        {
            runGeneration++;
            trackedRun = null;
            nextScan = 0f;
            reserves.Clear();
        }
    }
}
