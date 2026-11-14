using System;
using HarmonyLib;
using UnityEngine;
using EFT.CameraControl;

namespace TotalOptimization.Patches
{
    [HarmonyPatch(typeof(OpticCameraManager))]
    public static class OpticCameraPatch
    {
        [HarmonyPatch(nameof(OpticCameraManager.SetResolution))]
        [HarmonyPrefix]
        public static void PrefixSetResolution(ref int resolution)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigOptimizeOptics.Value) return;

            int maxRes = Plugin.ConfigMaxOpticResolution.Value;
            if (maxRes > 0 && resolution > maxRes)
            {
                resolution = maxRes;
            }
        }

        [HarmonyPatch(nameof(OpticCameraManager.OnOpticSightEnabled))]
        [HarmonyPostfix]
        public static void PostfixOnOpticEnabled(OpticCameraManager __instance)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigOptimizeOptics.Value) return;

            Camera cam = __instance.Camera;
            if (cam != null)
            {
                float maxDist = Plugin.ConfigOpticMaxDistance.Value;
                if (maxDist > 0f && cam.farClipPlane > maxDist)
                {
                    cam.farClipPlane = maxDist;
                }
            }
        }
    }
}
