using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using EFT;
using TotalOptimization.Patches;
using TotalOptimization.Telemetry;

namespace TotalOptimization.Patches
{
    [HarmonyPatch(typeof(BotItemTaker), nameof(BotItemTaker.ManualUpdate))]
    public static class BotItemTakerPatch
    {
        private class ThrottleState
        {
            public float NextSearchTime;
        }

        private static readonly ConditionalWeakTable<BotItemTaker, ThrottleState> _throttleTable =
            new ConditionalWeakTable<BotItemTaker, ThrottleState>();

        [HarmonyPrefix]
        public static bool Prefix(BotItemTaker __instance)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableItemTakerThrottling.Value)
            {
                return true;
            }

            BotOwner owner = __instance._owner;
            if (owner == null)
            {
                return true;
            }

            // 1. In combat suppression: bots should not scan for or pick up loose loot while fighting
            if (Plugin.ConfigCombatSuppressItemTaking.Value && owner.Memory != null && owner.Memory.HaveEnemy)
            {
                PerformanceTracker.CombatSuppressedLootChecks++;
                return false;
            }

            // If the bot has already locked onto an item to pick it up, allow it to finish
            if (__instance.HaveItemToTake())
            {
                return true;
            }

            // 2. Distance check: distant bots do not need frequent loot queries
            Player mainPlayer = GameWorldRegisterPatch.MainPlayer;
            if (mainPlayer != null)
            {
                float sqrDist = (owner.Position - mainPlayer.Position).sqrMagnitude;
                float maxDist = Plugin.ConfigItemTakerMaxDistance.Value;
                if (sqrDist > maxDist * maxDist)
                {
                    PerformanceTracker.ThrottledLootChecks++;
                    return false;
                }
            }

            // 3. Throttle loose loot scanning frequency
            ThrottleState state = _throttleTable.GetOrCreateValue(__instance);
            float now = Time.time;
            if (now < state.NextSearchTime)
            {
                PerformanceTracker.ThrottledLootChecks++;
                return false;
            }

            // Stagger next check with slight jitter
            state.NextSearchTime = now + Plugin.ConfigItemTakerInterval.Value + UnityEngine.Random.Range(0f, 0.3f);
            return true;
        }
    }
}
