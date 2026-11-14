using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace TotalOptimization.Controllers
{
    public class AntiStutterController : MonoBehaviour
    {
        public static AntiStutterController Instance { get; private set; }

        private float _combatCooldown = 0f;
        private bool _gcDisabled = false;

        private void Awake()
        {
            Instance = this;

            try
            {
                if (GarbageCollector.isIncremental)
                {
                    // Clamp incremental GC time slice to 1ms (1,000,000 nanoseconds) so it never causes frame drops
                    GarbageCollector.incrementalTimeSliceNanoseconds = 1000000;
                }
            }
            catch { }
        }

        public static void NotifyShotFired()
        {
            if (Instance != null)
            {
                Instance.OnShot();
            }
        }

        private void OnShot()
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigCombatGCGuard.Value)
            {
                return;
            }

            _combatCooldown = Time.time + 3.0f;

            if (!_gcDisabled)
            {
                try
                {
                    GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                    _gcDisabled = true;
                    if (Plugin.ConfigDebugLogging.Value)
                        Plugin.Log.LogInfo("[Total Optimization] Combat GC Guard: Disabled GC during gunfire.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[Total Optimization] Error disabling GC: {ex.Message}");
                }
            }
        }

        private void Update()
        {
            if (_gcDisabled && Time.time > _combatCooldown)
            {
                try
                {
                    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                    _gcDisabled = false;
                    if (Plugin.ConfigDebugLogging.Value)
                        Plugin.Log.LogInfo("[Total Optimization] Combat GC Guard: Gunfire ended, re-enabled GC.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[Total Optimization] Error re-enabling GC: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            if (_gcDisabled)
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
            }
        }
    }
}
