using System;
using System.Reflection;
using HarmonyLib;
using EFT;

namespace TotalOptimization.Patches
{
    /// <summary>
    /// Intercepts Smoothie's HandsCulling.Apply method to ensure it never turns off
    /// the weapon renderers on the local human player (PMC or Scav).
    /// Smoothie's HandsCulling erroneously ran for the local player because OfflinePlayerCulling
    /// occludes the 1st person player body during sprint, turning off the weapon in hands.
    /// </summary>
    public static class SmoothieSafetyPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                Type handsCulling = AccessTools.TypeByName("Smoothie.HandsCulling");
                if (handsCulling != null)
                {
                    MethodInfo applyMethod = AccessTools.Method(handsCulling, "Apply", new Type[] { typeof(Player), typeof(bool) });
                    if (applyMethod != null)
                    {
                        MethodInfo prefix = AccessTools.Method(typeof(SmoothieSafetyPatch), nameof(ApplyPrefix));
                        harmony.Patch(applyMethod, prefix: new HarmonyMethod(prefix));
                        Plugin.Log.LogInfo("[Total Optimization] Successfully protected Player weapons against Smoothie.HandsCulling!");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogWarning($"[Total Optimization] Smoothie patch note: {ex.Message}");
            }
        }

        public static bool ApplyPrefix(Player player, bool hide)
        {
            if (player != null && player.IsYourPlayer)
            {
                // NEVER hide hands or weapon for the local player!
                return false;
            }
            return true;
        }
    }
}
