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
        public const string PluginVersion = "0.1.0";
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
        private ConfigEntry<bool> enabled;
        private ConfigEntry<string> startingReserve;
        private ConfigEntry<float> switchDelay;
        private ConfigEntry<bool> verboseLogging;

        private void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true,
                "Enable server-authoritative automatic equipment switching.");
            startingReserve = Config.Bind("Prototype", "StartingReserveEquipment", "DroneBackup",
                "Internal equipment name for a test reserve. Empty disables the test starter.");
            switchDelay = Config.Bind("Switching", "DelaySeconds", 0.3f,
                new ConfigDescription("Delay after successful activation before swapping.",
                    new AcceptableValueRange<float>(0f, 2f)));
            verboseLogging = Config.Bind("Diagnostics", "VerboseLogging", false,
                "Log reserve seeding and swaps.");
            On.RoR2.EquipmentSlot.PerformEquipmentAction += OnPerformEquipmentAction;
            Logger.LogInfo("DualGear v" + PluginVersion + " loaded (prototype).");
        }

        private void OnDestroy()
        {
            On.RoR2.EquipmentSlot.PerformEquipmentAction -= OnPerformEquipmentAction;
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
            if (!enabled.Value || Time.unscaledTime < nextScan) return;
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
            reserve.Equipment = new EquipmentState(index, 0f, 1, false);
            reserve.HasEquipment = true;
            if (verboseLogging.Value) Logger.LogInfo("Seeded reserve for " + master.name);
        }

        private bool OnPerformEquipmentAction(
            On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig,
            EquipmentSlot slot, EquipmentIndex equipmentIndex)
        {
            bool success = orig(slot, equipmentIndex);
            if (!success || !NetworkServer.active || !enabled.Value || trackedRun == null ||
                slot == null || slot.characterBody == null) return success;

            CharacterMaster master = slot.characterBody.master;
            if (master == null || !reserves.TryGetValue(master, out Reserve reserve) ||
                !SwapPolicy.ShouldQueue(success, reserve.HasEquipment, reserve.SwapPending))
                return success;

            reserve.SwapPending = true;
            StartCoroutine(DeferredSwap(master, reserve, equipmentIndex, runGeneration));
            return success;
        }

        private IEnumerator DeferredSwap(CharacterMaster master, Reserve reserve,
            EquipmentIndex activatedIndex, int expectedGeneration)
        {
            yield return new WaitForSeconds(switchDelay.Value);
            try
            {
                if (!NetworkServer.active || !enabled.Value || trackedRun == null ||
                    runGeneration != expectedGeneration || master == null || master.inventory == null ||
                    !reserves.TryGetValue(master, out Reserve stored) ||
                    !ReferenceEquals(stored, reserve) || !reserve.HasEquipment)
                    yield break;

                Inventory inventory = master.inventory;
                uint slot = (uint)inventory.activeEquipmentSlot;
                EquipmentState current = inventory.GetEquipment(slot);
                if (!SwapPolicy.CanCommit(
                    trackedRun != null && runGeneration == expectedGeneration,
                    current.equipmentIndex == activatedIndex, reserve.HasEquipment))
                    yield break;

                EquipmentState next = reserve.Equipment;
                if (next.equipmentIndex == EquipmentIndex.None) yield break;
                inventory.SetEquipment(next, slot);
                if (inventory.GetEquipment(slot).equipmentIndex != next.equipmentIndex)
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
