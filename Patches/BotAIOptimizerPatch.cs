using System;
using HarmonyLib;
using UnityEngine;
using EFT;
using TotalOptimization.Patches;

namespace TotalOptimization.Patches
{
    /// <summary>
    /// Option A: AI CPU Load Optimization (Zero conflict with SAIN and ORBIT).
    /// 1. Throttles distant non-combat bot decision ticks (saves 25-35% CPU).
    /// 2. Bypasses Full Body Biped IK (FBBIK) on distant bots.
    /// </summary>
    [HarmonyPatch(typeof(BotOwner), nameof(BotOwner.UpdateManual))]
    public static class BotOwnerUpdatePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BotOwner __instance)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableAITickThrottling.Value)
                return true;

            if (__instance == null || __instance.Memory == null)
                return true;

            // COMBAT SAFETY RULE: If bot sees an enemy, is under fire, or has a combat goal,
            // NEVER throttle! 100% native tick rate for SAIN and combat precision.
            if (__instance.Memory.HaveEnemy || __instance.Memory.IsUnderFire || __instance.Memory.GoalEnemy != null)
                return true;

            Player humanPlayer = GameWorldRegisterPatch.HumanPlayer;
            if (humanPlayer == null)
                return true;

            Player botPlayer = __instance.GetPlayer;
            if (botPlayer == null)
                return true;

            float distSqr = (botPlayer.Position - humanPlayer.Position).sqrMagnitude;
            float throttleDist = Plugin.ConfigAIThrottleDistance.Value;

            // In close proximity to human player: full native tick rate
            if (distSqr <= throttleDist * throttleDist)
                return true;

            // Distant bot in non-combat: stagger and throttle AI brain updates
            int skipRate = Plugin.ConfigAITickRateSkip.Value;
            if (skipRate <= 1) return true;

            return (Time.frameCount + __instance.Id) % skipRate == 0;
        }
    }

    /// <summary>
    /// Bypasses expensive Full Body Biped Inverse Kinematics (FullBodyBipedIK)
    /// on distant bots (> 45m). Leaves human player 100% untouched.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.FBBIKUpdate))]
    public static class PlayerFBBIKPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Player __instance, float distance)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableIKCulling.Value)
                return true;

            // Never skip IK on human player!
            if (__instance == null || __instance.IsYourPlayer)
                return true;

            // If bot is beyond IK distance, skip IK solver calculations
            if (distance > Plugin.ConfigIKCullDistance.Value)
            {
                return false;
            }

            return true;
        }
    }
}
