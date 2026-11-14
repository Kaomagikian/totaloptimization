using System;
using UnityEngine;
using TotalOptimization.Components;

namespace TotalOptimization.Controllers
{
    public static class EngineTuner
    {
        private static float _lastEnforceTime = 0f;
        private static bool _wasDisabled = false;
        private static bool _initialized = false;

        public static void ApplySettings()
        {
            try
            {
                if (!Plugin.ConfigEnabled.Value)
                {
                    RevertToVanilla();
                    return;
                }

                _wasDisabled = false;

                // 1. Physics Tuning
                if (Plugin.ConfigTunePhysics.Value)
                {
                    Physics.reuseCollisionCallbacks = true;
                    Physics.queriesHitBackfaces = false;
                }
                else
                {
                    Physics.reuseCollisionCallbacks = false;
                    Physics.queriesHitBackfaces = true;
                }

                // 2. Texture & Asset Streaming Buffer
                if (Plugin.ConfigTuneAsyncUpload.Value)
                {
                    QualitySettings.asyncUploadBufferSize = 128; // 128 MB buffer
                    QualitySettings.asyncUploadTimeSlice = 4;     // 4 ms slice
                    QualitySettings.asyncUploadPersistentBuffer = true;
                }

#pragma warning disable 618
                // 3. Texture Downscaling / RAM Relief (Set ONCE at load, never in a loop!)
                if (Plugin.ConfigHalfResTextures.Value)
                {
                    if (QualitySettings.masterTextureLimit != 1)
                    {
                        QualitySettings.masterTextureLimit = 1;
                        QualitySettings.globalTextureMipmapLimit = 1;
                    }
                }
                else
                {
                    if (QualitySettings.masterTextureLimit != 0)
                    {
                        QualitySettings.masterTextureLimit = 0;
                        QualitySettings.globalTextureMipmapLimit = 0;
                    }
                }
#pragma warning restore 618

                // 4. Texture Mipmap Streaming
                if (Plugin.ConfigEnableTextureStreaming.Value)
                {
                    QualitySettings.streamingMipmapsActive = true;
                    QualitySettings.streamingMipmapsMemoryBudget = Plugin.ConfigTextureStreamingBudgetMB.Value;
                    QualitySettings.streamingMipmapsMaxLevelReduction = 2;
                    QualitySettings.streamingMipmapsAddAllCameras = true;
                    QualitySettings.streamingMipmapsRenderersPerFrame = 256;
                }
                else
                {
                    QualitySettings.streamingMipmapsActive = false;
                }

                // 5. LOD Bias (Reduces polycount of distant objects)
                if (Plugin.ConfigTuneLODBias.Value)
                {
                    QualitySettings.lodBias = Plugin.ConfigLODBiasValue.Value;
                }

                // 6. Particle Collision Raycasts
                if (Plugin.ConfigTuneParticleBudget.Value)
                {
                    QualitySettings.particleRaycastBudget = Plugin.ConfigParticleRaycastLimit.Value;
                }

                // 7. Real-time Reflection Probes
                if (Plugin.ConfigDisableRealtimeReflections.Value)
                {
                    QualitySettings.realtimeReflectionProbes = false;
                }

                // 8. Skin Weights: Always FourBones so weapons, attachments, and Scav rigs never collapse
                QualitySettings.skinWeights = SkinWeights.FourBones;

                // 9. Shadow Tuning (Permanently enforced against EFT overrides)
                ApplyShadowSettings();

                // 11. Camera Far Clip Plane
                ClampCameraFarClip();

                // 12. Terrain & Foliage Optimization
                TerrainOptimizer.ApplySettings();

                if (!_initialized && Plugin.ConfigDebugLogging.Value)
                {
                    Plugin.Log.LogInfo("[Total Optimization] Engine & Quality tuning applied successfully.");
                    _initialized = true;
                }
            }
            catch (Exception ex)
            {
                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogError($"[Total Optimization] Error applying Engine settings: {ex.Message}");
            }
        }

        public static void ClampCameraFarClip()
        {
            if (!Plugin.ConfigClampViewDistance.Value) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                float targetFar = Plugin.ConfigMaxViewDistance.Value;
                if (mainCam.farClipPlane > targetFar)
                {
                    mainCam.farClipPlane = targetFar;
                }
            }
        }

        public static void RevertToVanilla()
        {
            if (_wasDisabled) return;
            _wasDisabled = true;

            try
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowCascades = 4;
                QualitySettings.shadowDistance = 120.0f;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.skinWeights = SkinWeights.FourBones;
                QualitySettings.streamingMipmapsActive = false;
#pragma warning disable 618
                QualitySettings.masterTextureLimit = 0;
                QualitySettings.globalTextureMipmapLimit = 0;
#pragma warning restore 618
                QualitySettings.lodBias = 2.0f;
                Physics.reuseCollisionCallbacks = false;
                Physics.queriesHitBackfaces = true;

                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.farClipPlane = 1500f;
                }

                // Restore all bots to vanilla always-animate state
                BotOptimizerComponent.RestoreAllBots();

                // Restore terrain settings
                TerrainOptimizer.RevertToVanilla();

                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogInfo("[Total Optimization] Reverted all engine & bot settings back to vanilla.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Total Optimization] Error reverting to vanilla: {ex.Message}");
            }
        }

        public static void ApplyShadowSettings()
        {
            if (!Plugin.ConfigEnableShadowTuning.Value)
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowCascades = 4;
                QualitySettings.shadowDistance = 120.0f;
                return;
            }

            // Completely disable shadows if requested
            if (Plugin.ConfigDisableShadowsCompletely.Value)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                return;
            }

            QualitySettings.shadows = ShadowQuality.All;

            // Cascades (default in EFT is 4, reducing to 2 cuts shadow draw calls by half)
            int cascades = Plugin.ConfigShadowCascades.Value;
            QualitySettings.shadowCascades = (cascades == 1 || cascades == 2 || cascades == 4) ? cascades : 2;

            // Max shadow distance (clamp to avoid rendering distant shadow maps)
            float maxDist = Plugin.ConfigMaxShadowDistance.Value;
            if (maxDist > 0f)
            {
                QualitySettings.shadowDistance = maxDist;
            }

            // Shadow resolution
            int resChoice = Plugin.ConfigShadowResolution.Value;
            if (resChoice == 0) QualitySettings.shadowResolution = ShadowResolution.Low;
            else if (resChoice == 1) QualitySettings.shadowResolution = ShadowResolution.Medium;
            else if (resChoice == 2) QualitySettings.shadowResolution = ShadowResolution.High;
        }

        /// <summary>
        /// Lightweight check (does NOT touch texture limits to prevent 1-FPS reloads).
        /// </summary>
        public static void CheckAndEnforce()
        {
            if (!Plugin.ConfigEnabled.Value)
            {
                if (!_wasDisabled) RevertToVanilla();
                return;
            }

            if (Time.unscaledTime - _lastEnforceTime < 3.0f) return;
            _lastEnforceTime = Time.unscaledTime;

            if (Plugin.ConfigEnableShadowTuning.Value)
            {
                if (!Plugin.ConfigDisableShadowsCompletely.Value)
                {
                    if (QualitySettings.shadowCascades != Plugin.ConfigShadowCascades.Value)
                    {
                        QualitySettings.shadowCascades = Plugin.ConfigShadowCascades.Value;
                    }
                    if (QualitySettings.shadowDistance > Plugin.ConfigMaxShadowDistance.Value + 1f)
                    {
                        QualitySettings.shadowDistance = Plugin.ConfigMaxShadowDistance.Value;
                    }
                }
            }

            if (Plugin.ConfigTunePhysics.Value)
            {
                if (!Physics.reuseCollisionCallbacks) Physics.reuseCollisionCallbacks = true;
                if (Physics.queriesHitBackfaces) Physics.queriesHitBackfaces = false;
            }

            ClampCameraFarClip();
        }
    }
}
