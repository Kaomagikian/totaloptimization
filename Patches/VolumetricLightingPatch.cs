using System;
using HarmonyLib;
using UnityEngine;

namespace TotalOptimization.Patches
{
    // 1. Volumetric Light Renderer
    [HarmonyPatch(typeof(VolumetricLightRenderer))]
    public static class VolumetricLightRendererPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void PostfixAwake(VolumetricLightRenderer __instance)
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricLighting.Value)
            {
                __instance.enabled = false;
                __instance.IsOn = false;
            }
        }

        [HarmonyPatch(nameof(VolumetricLightRenderer.OnPreRender))]
        [HarmonyPrefix]
        public static bool PrefixPreRender()
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricLighting.Value)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(VolumetricLightRenderer.InitPostRenderPass))]
        [HarmonyPrefix]
        public static bool PrefixPostRender()
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricLighting.Value)
            {
                return false;
            }
            return true;
        }
    }

    // 2. Individual Volumetric Light Beams
    [HarmonyPatch(typeof(VolumetricLight))]
    public static class VolumetricLightPreRenderPatch
    {
        [HarmonyPatch(nameof(VolumetricLight.VolumetricLightPreRender))]
        [HarmonyPrefix]
        public static bool PrefixPreRender()
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricLighting.Value)
            {
                return false;
            }
            return true;
        }
    }

    // 3. Volumetric Fog Compute Shader Raymarching
    [HarmonyPatch(typeof(VolumetricFog))]
    public static class VolumetricFogPatch
    {
        [HarmonyPatch("InitResources")]
        [HarmonyPrefix]
        public static bool PrefixInitResources()
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricFog.Value)
            {
                return false; // Prevents allocating heavy compute buffers in VRAM
            }
            return true;
        }

        [HarmonyPatch(nameof(VolumetricFog.OnPreRender))]
        [HarmonyPrefix]
        public static bool PrefixPreRender()
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricFog.Value)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(nameof(VolumetricFog.OnRenderImage))]
        [HarmonyPrefix]
        public static bool PrefixRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (Plugin.ConfigEnabled.Value && Plugin.ConfigDisableVolumetricFog.Value)
            {
                Graphics.Blit(src, dest);
                return false;
            }
            return true;
        }
    }
}
