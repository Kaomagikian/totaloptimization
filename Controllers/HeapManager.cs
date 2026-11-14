using System;
using UnityEngine;
using Comfort.Common;
using EFT;
using TotalOptimization.Components;
using TotalOptimization.Patches;

namespace TotalOptimization.Controllers
{
    public class HeapManager : MonoBehaviour
    {
        private bool _wasInRaid = false;

        private void Update()
        {
            bool inRaid = Singleton<GameWorld>.Instantiated;

            // Transition from raid back to menu / hideout
            if (_wasInRaid && !inRaid)
            {
                OnReturnToMenu();
            }
            _wasInRaid = inRaid;
        }

        private void OnReturnToMenu()
        {
            try
            {
                // 1. Reset all state cleanly so raid 2 never hangs on "Game Preparation"
                Plugin.Instance?.ResetRaidState();
                GameWorldRegisterPatch.ResetState();
                BotOptimizerComponent.ActiveBots.Clear();

                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogInfo("[Total Optimization] Raid ended. Cleaning managed heap & resetting state for next raid...");

                // 2. Safe memory and asset cleanup between raids
                Resources.UnloadUnusedAssets();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (Plugin.ConfigDebugLogging.Value)
                {
                    long currentHeapMb = GC.GetTotalMemory(false) / (1024 * 1024);
                    Plugin.Log.LogInfo($"[Total Optimization] Heap cleaned successfully. Managed memory now at {currentHeapMb} MB.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Total Optimization] Error on return to menu: {ex.Message}");
            }
        }
    }
}
