using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using EFT;

namespace TotalOptimization.Patches
{
    [HarmonyPatch(typeof(Shell), nameof(Shell.ActivatePhysics))]
    public static class ShellCasingPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Shell __instance)
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigOptimizeShellCasings.Value) return;

            if (__instance != null && __instance.gameObject.activeInHierarchy)
            {
                __instance.StartCoroutine(DisableShellPhysicsAfterDelay(__instance, 2.5f));
            }
        }

        private static IEnumerator DisableShellPhysicsAfterDelay(Shell shell, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (shell != null)
            {
                try
                {
                    shell.DisablePhysics();
                }
                catch { }
            }
        }
    }
}
