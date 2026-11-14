using System;
using UnityEngine;

namespace TotalOptimization.Controllers
{
    public static class TerrainOptimizer
    {
        public static void ApplySettings()
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigOptimizeTerrain.Value)
            {
                return;
            }

            try
            {
                Terrain[] terrains = Terrain.activeTerrains;
                if (terrains == null || terrains.Length == 0) return;

                float detailDist = Plugin.ConfigDetailDistance.Value;
                float detailDensity = Plugin.ConfigDetailDensity.Value;
                float heightmapError = Plugin.ConfigHeightmapPixelError.Value;
                float treeDist = Plugin.ConfigTreeDistance.Value;
                float billboardDist = Plugin.ConfigTreeBillboardDistance.Value;
                int fullLODCount = Plugin.ConfigTreeFullLODCount.Value;

                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain t = terrains[i];
                    if (t == null) continue;

                    t.drawInstanced = true;
                    t.detailObjectDistance = detailDist;
                    t.detailObjectDensity = detailDensity;
                    t.heightmapPixelError = heightmapError;
                    t.treeDistance = treeDist;
                    t.treeBillboardDistance = billboardDist;
                    t.treeMaximumFullLODCount = fullLODCount;
                }

                if (Plugin.ConfigDebugLogging.Value)
                {
                    Plugin.Log.LogInfo($"[Total Optimization] Optimized {terrains.Length} active terrains (Grass: {detailDist}m, Trees: {treeDist}m, PixelError: {heightmapError}).");
                }
            }
            catch (Exception ex)
            {
                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogError($"[Total Optimization] Error optimizing terrain: {ex.Message}");
            }
        }

        public static void RevertToVanilla()
        {
            try
            {
                Terrain[] terrains = Terrain.activeTerrains;
                if (terrains == null || terrains.Length == 0) return;

                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain t = terrains[i];
                    if (t == null) continue;

                    t.detailObjectDistance = 100f;
                    t.detailObjectDensity = 1.0f;
                    t.heightmapPixelError = 1.0f;
                    t.treeDistance = 1000f;
                    t.treeBillboardDistance = 200f;
                    t.treeMaximumFullLODCount = 300;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Total Optimization] Error reverting terrain: {ex.Message}");
            }
        }
    }
}
