using System;
using HarmonyLib;
using UnityEngine;
using TotalOptimization.Controllers;

namespace TotalOptimization.Patches
{
    [HarmonyPatch(typeof(MuzzleLight))]
    public static class MuzzleLightPatch
    {
        [HarmonyPatch(nameof(MuzzleLight.Start))]
        [HarmonyPostfix]
        public static void PostfixStart(MuzzleLight __instance)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigDisableMuzzleShadows.Value) return;

            StripShadows(__instance);
        }

        [HarmonyPatch(nameof(MuzzleLight.OnShot))]
        [HarmonyPrefix]
        public static void PrefixOnShot(MuzzleLight __instance)
        {
            if (!Plugin.ConfigEnabled.Value) return;

            if (Plugin.ConfigDisableMuzzleShadows.Value)
            {
                StripShadows(__instance);
            }

            AntiStutterController.NotifyShotFired();
        }

        private static void StripShadows(MuzzleLight muzzleLight)
        {
            if (muzzleLight == null || muzzleLight.Lights == null) return;

            for (int i = 0; i < muzzleLight.Lights.Length; i++)
            {
                Light l = muzzleLight.Lights[i];
                if (l != null && l.shadows != LightShadows.None)
                {
                    l.shadows = LightShadows.None;
                }
            }
        }
    }
}
