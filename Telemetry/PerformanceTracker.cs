using System;
using System.Collections.Generic;
using UnityEngine;
using Comfort.Common;
using EFT;
using TotalOptimization.Components;

namespace TotalOptimization.Telemetry
{
    public class PerformanceTracker : MonoBehaviour
    {
        private float _lastLogTime;
        private int _frameCount;
        private float _timeAccumulator;
        private readonly List<float> _frameTimes = new List<float>(500);

        public static int ThrottledLootChecks { get; set; }
        public static int CombatSuppressedLootChecks { get; set; }

        private float _currentFps;
        private float _onePercentLow;

        private void Start()
        {
            _lastLogTime = Time.time;
        }

        public static int GetAliveBotCount()
        {
            if (Singleton<GameWorld>.Instantiated)
            {
                var gw = Singleton<GameWorld>.Instance;
                if (gw != null && gw.AllAlivePlayersList != null)
                {
                    int count = 0;
                    for (int i = 0; i < gw.AllAlivePlayersList.Count; i++)
                    {
                        Player p = gw.AllAlivePlayersList[i];
                        if (p != null && !p.IsYourPlayer && p.HealthController != null && p.HealthController.IsAlive)
                        {
                            count++;
                        }
                    }
                    return count;
                }
            }
            return BotOptimizerComponent.ActiveBots.Count;
        }

        private void Update()
        {
            if (!Plugin.ConfigShowFpsOverlay.Value && !Plugin.ConfigDebugLogging.Value)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            _frameTimes.Add(dt);
            _timeAccumulator += dt;
            _frameCount++;

            // Calculate FPS metrics every 1 second
            if (_timeAccumulator >= 1.0f)
            {
                _currentFps = _frameCount / _timeAccumulator;

                // 1% Low calculation
                _frameTimes.Sort();
                int onePercentIndex = Mathf.Max(0, (int)(_frameTimes.Count * 0.99f));
                float onePercentWorstDt = _frameTimes[Mathf.Min(onePercentIndex, _frameTimes.Count - 1)];
                _onePercentLow = onePercentWorstDt > 0f ? (1f / onePercentWorstDt) : _currentFps;

                _frameTimes.Clear();
                _frameCount = 0;
                _timeAccumulator = 0f;
            }

            // Periodic performance log in raid - ONLY if DebugLogging is enabled
            if (Plugin.ConfigDebugLogging.Value && Singleton<GameWorld>.Instantiated)
            {
                if (Time.time - _lastLogTime >= Plugin.ConfigMetricsInterval.Value)
                {
                    _lastLogTime = Time.time;
                    LogMetrics();
                }
            }
        }

        private void LogMetrics()
        {
            int aliveBots = GetAliveBotCount();
            long heapMb = GC.GetTotalMemory(false) / (1024 * 1024);

            Plugin.Log.LogInfo(
                $"[Total Optimization] FPS: {_currentFps:F1} (1% Low: {_onePercentLow:F1}) | " +
                $"Alive Bots: {aliveBots} | " +
                $"Loot Scans Throttled: {ThrottledLootChecks} | Combat Suppressed: {CombatSuppressedLootChecks} | " +
                $"Heap: {heapMb} MB"
            );
        }

        private void OnGUI()
        {
            if (!Plugin.ConfigShowFpsOverlay.Value || !Singleton<GameWorld>.Instantiated)
            {
                return;
            }

            int aliveBots = GetAliveBotCount();
            string text = $"FPS: {_currentFps:F0} (1% Low: {_onePercentLow:F0}) | Bots: {aliveBots}";

            GUI.color = Color.black;
            GUI.Label(new Rect(11, 11, 350, 25), text);
            GUI.color = Color.green;
            GUI.Label(new Rect(10, 10, 350, 25), text);
        }
    }
}
