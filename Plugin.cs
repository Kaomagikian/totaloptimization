using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using TotalOptimization.Controllers;
using TotalOptimization.Telemetry;
using TotalOptimization.Patches;

namespace TotalOptimization
{
    [BepInPlugin("com.ravein.totaloptimization", "Total Optimization", "1.8.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log => Instance?.Logger;

        // 1. General & Telemetry
        public static ConfigEntry<bool> ConfigEnabled;
        public static ConfigEntry<bool> ConfigDebugLogging;
        public static ConfigEntry<bool> ConfigShowFpsOverlay;
        public static ConfigEntry<float> ConfigMetricsInterval;

        // 2. De-Clutterer (Trash, Puddles, Cigarettes, Debris)
        public static ConfigEntry<bool> ConfigEnableDeClutterer;
        public static ConfigEntry<bool> ConfigRemoveSmallDebrisBySize;
        public static ConfigEntry<float> ConfigMaxDebrisSize;

        // 3. Texture Clamping (Hard limit <= 1024x1024)
        public static ConfigEntry<bool> ConfigEnableTextureClamping;
        public static ConfigEntry<int> ConfigMaxTextureResolution;
        public static ConfigEntry<bool> ConfigHalfResTextures;
        public static ConfigEntry<bool> ConfigEnableTextureStreaming;
        public static ConfigEntry<float> ConfigTextureStreamingBudgetMB;

        // 4. Loose Loot Optimization
        public static ConfigEntry<bool> ConfigEnableLootCulling;
        public static ConfigEntry<float> ConfigLootCullDistance;
        public static ConfigEntry<bool> ConfigEnableLootFovCulling;

        // 5. Anti-Shoot-Stutter
        public static ConfigEntry<bool> ConfigDisableMuzzleShadows;
        public static ConfigEntry<bool> ConfigCombatGCGuard;
        public static ConfigEntry<bool> ConfigOptimizeShellCasings;

        // 6. Unity Engine Tuning
        public static ConfigEntry<bool> ConfigTunePhysics;
        public static ConfigEntry<bool> ConfigTuneAsyncUpload;
        public static ConfigEntry<bool> ConfigTuneParticleBudget;
        public static ConfigEntry<int> ConfigParticleRaycastLimit;
        public static ConfigEntry<bool> ConfigDisableRealtimeReflections;
        public static ConfigEntry<bool> ConfigOptimizeSkinWeights;

        // 7. Draw Distance & LOD Bias
        public static ConfigEntry<bool> ConfigClampViewDistance;
        public static ConfigEntry<float> ConfigMaxViewDistance;
        public static ConfigEntry<bool> ConfigTuneLODBias;
        public static ConfigEntry<float> ConfigLODBiasValue;

        // 8. Shadow & Rendering Tuning
        public static ConfigEntry<bool> ConfigEnableShadowTuning;
        public static ConfigEntry<bool> ConfigDisableShadowsCompletely;
        public static ConfigEntry<int> ConfigShadowCascades;
        public static ConfigEntry<float> ConfigMaxShadowDistance;
        public static ConfigEntry<int> ConfigShadowResolution;

        // 9. Volumetric Lighting & Fog
        public static ConfigEntry<bool> ConfigDisableVolumetricLighting;
        public static ConfigEntry<bool> ConfigDisableVolumetricFog;

        // 10. Optics & Scopes (Picture-in-Picture)
        public static ConfigEntry<bool> ConfigOptimizeOptics;
        public static ConfigEntry<int> ConfigMaxOpticResolution;
        public static ConfigEntry<float> ConfigOpticMaxDistance;

        // 11. AI Bot CPU Optimization (Option A - Zero SAIN/ORBIT conflict)
        public static ConfigEntry<bool> ConfigEnableAITickThrottling;
        public static ConfigEntry<float> ConfigAIThrottleDistance;
        public static ConfigEntry<int> ConfigAITickRateSkip;
        public static ConfigEntry<bool> ConfigEnableIKCulling;
        public static ConfigEntry<float> ConfigIKCullDistance;
        public static ConfigEntry<bool> ConfigEnableAnimatorCulling;

        // 12. Weapon & Inventory Throttling
        public static ConfigEntry<bool> ConfigEnableItemTakerThrottling;
        public static ConfigEntry<bool> ConfigCombatSuppressItemTaking;
        public static ConfigEntry<float> ConfigItemTakerInterval;
        public static ConfigEntry<float> ConfigItemTakerMaxDistance;

        // 13. Heap & Working Set Management
        public static ConfigEntry<bool> ConfigTrimWorkingSetOnRaidEnd;

        // 14. Terrain & Foliage
        public static ConfigEntry<bool> ConfigOptimizeTerrain;
        public static ConfigEntry<float> ConfigDetailDistance;
        public static ConfigEntry<float> ConfigDetailDensity;
        public static ConfigEntry<float> ConfigHeightmapPixelError;
        public static ConfigEntry<float> ConfigTreeDistance;
        public static ConfigEntry<float> ConfigTreeBillboardDistance;
        public static ConfigEntry<int> ConfigTreeFullLODCount;

        // 15. RAM & Memory Management (Options 1 & 3)
        public static ConfigEntry<bool> ConfigEnablePostLoadPurge;
        public static ConfigEntry<bool> ConfigEnableAudioPurge;

        private bool _raidRoutinesRunning = false;

        private void Awake()
        {
            Instance = this;

            InitConfig();

            ApplyHarmonyPatches();

            // Attach Controllers to plugin GameObject
            gameObject.AddComponent<HeapManager>();
            gameObject.AddComponent<PerformanceTracker>();
            gameObject.AddComponent<AntiStutterController>();
            gameObject.AddComponent<LootCuller>();

            // Apply engine optimizations initially
            EngineTuner.ApplySettings();

            Log.LogInfo("[Total Optimization] Plugin v1.8.0 loaded successfully!");
        }

        public void TriggerRaidStartRoutines()
        {
            if (_raidRoutinesRunning) return;
            StartCoroutine(RaidStartOptimizationRoutine());
        }

        private IEnumerator RaidStartOptimizationRoutine()
        {
            _raidRoutinesRunning = true;

            // Wait 2.5 seconds until human player and world objects are fully established
            yield return new WaitForSeconds(2.5f);

            // 1. Terrain & Foliage optimization
            TerrainOptimizer.ApplySettings();

            // 2. Safe static De-Clutterer (puddles, trash, cigarettes)
            DeClutterer.RunDeClutter();

            // 3. Audio Memory Purge (Option 3: unloads ambient / menu / long music clips from RAM)
            AudioOptimizer.PurgeUnusedAudio();

            // 4. GC Cleanup (Safe in-raid garbage collection without unloading active assets)
            if (ConfigEnablePostLoadPurge.Value)
            {
                GC.Collect();
            }

            // 5. Texture Mip Clamp (Option 4: discards unneeded mipmaps once per raid)
            TextureClampController.ClampAllTextures();

            _raidRoutinesRunning = false;
        }

        public void ResetRaidState()
        {
            _raidRoutinesRunning = false;
            TextureClampController.ResetState();
        }

        private void Update()
        {
            // Continuously verify and enforce settings so EFT cannot silently revert them
            EngineTuner.CheckAndEnforce();
        }

        private void ApplyHarmonyPatches()
        {
            Harmony harmony = new Harmony("com.ravein.totaloptimization");

            Type[] patchTypes = new Type[]
            {
                typeof(GameWorldRegisterPatch),
                typeof(BotItemTakerPatch),
                typeof(BotOwnerUpdatePatch),
                typeof(PlayerFBBIKPatch),
                typeof(MuzzleLightPatch),
                typeof(ShellCasingPatch),
                typeof(OpticCameraPatch),
                typeof(VolumetricLightRendererPatch),
                typeof(VolumetricLightPreRenderPatch),
                typeof(VolumetricFogPatch)
            };

            int successCount = 0;
            foreach (Type patchType in patchTypes)
            {
                try
                {
                    harmony.CreateClassProcessor(patchType).Patch();
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.LogError($"[Total Optimization] Patch {patchType.Name} failed: {ex.Message}");
                }
            }

            Log.LogInfo($"[Total Optimization] Applied {successCount}/{patchTypes.Length} patch sets.");

            // Apply Smoothie safety patch
            SmoothieSafetyPatch.TryPatch(harmony);
        }

        private void InitConfig()
        {
            // 1. General & Telemetry (Defaults OFF as requested)
            ConfigEnabled = Config.Bind("1. General", "Enabled", true, "Enable or disable all optimizations globally.");
            ConfigDebugLogging = Config.Bind("1. General", "DebugLogging", false, "Enable verbose debug logging in BepInEx console.");
            ConfigShowFpsOverlay = Config.Bind("1. General", "ShowFpsOverlay", false, "Displays in-raid FPS and Bot Culling counter in top-left corner.");
            ConfigMetricsInterval = Config.Bind("1. General", "MetricsInterval", 20.0f, new ConfigDescription("Interval in seconds to print performance metrics to BepInEx log (when DebugLogging is true).", new AcceptableValueRange<float>(5.0f, 120.0f)));

            // 2. De-Clutterer (Trash, Puddles, Cigarettes, Debris)
            ConfigEnableDeClutterer = Config.Bind(
                "2. De-Clutterer",
                "EnableDeClutterer",
                true,
                "Removes tens of thousands of puddles, cigarette butts, garbage piles, tin cans, cardboard scraps, and tiny decorative debris to dramatically save RAM and draw calls."
            );
            ConfigRemoveSmallDebrisBySize = Config.Bind(
                "2. De-Clutterer",
                "RemoveSmallDebrisBySize",
                true,
                "Removes tiny non-interactive clutter objects based on physical 3D bounds volume."
            );
            ConfigMaxDebrisSize = Config.Bind(
                "2. De-Clutterer",
                "MaxDebrisSize",
                0.35f,
                new ConfigDescription("Maximum dimension in meters for small debris culling (default 0.35m = 35cm).", new AcceptableValueRange<float>(0.10f, 1.0f))
            );

            // 3. Texture Clamping (Hard limit <= 1024x1024)
            ConfigEnableTextureClamping = Config.Bind(
                "3. Texture Clamping",
                "EnableTextureClamping",
                true,
                "Forces all loaded textures to never exceed maximum resolution, immediately purging high-res 2K/4K mipmaps from RAM and VRAM."
            );
            ConfigMaxTextureResolution = Config.Bind(
                "3. Texture Clamping",
                "MaxTextureResolution",
                1024,
                new ConfigDescription("Hard cap on texture resolution (1024 recommended for 16GB RAM).", new AcceptableValueList<int>(512, 1024, 2048))
            );
            ConfigHalfResTextures = Config.Bind(
                "3. Texture Clamping",
                "HalfResTextures",
                true,
                "Downscales textures by 1 mip level on the fly (masterTextureLimit = 1). Slashes texture RAM footprint from 14 GB down to 3.5 GB."
            );
            ConfigEnableTextureStreaming = Config.Bind(
                "3. Texture Clamping",
                "EnableTextureStreaming",
                true,
                "Enforces Unity native Mipmap Streaming to prevent loading full-resolution textures across the entire 3km map at once."
            );
            ConfigTextureStreamingBudgetMB = Config.Bind(
                "3. Texture Clamping",
                "TextureStreamingBudgetMB",
                2048.0f,
                new ConfigDescription("VRAM / RAM texture streaming memory budget in megabytes (e.g. 2048 MB).", new AcceptableValueRange<float>(1024.0f, 6144.0f))
            );

            // 4. Loose Loot Optimization
            ConfigEnableLootCulling = Config.Bind(
                "4. Loose Loot Culling",
                "EnableLootCulling",
                true,
                "Hides 3D renderers of loose loot items further than 40 meters away, saving hundreds of item render calls across huge maps."
            );
            ConfigLootCullDistance = Config.Bind(
                "4. Loose Loot Culling",
                "LootCullDistance",
                40.0f,
                new ConfigDescription("Distance in meters beyond which loose loot is visually culled until player approaches.", new AcceptableValueRange<float>(15.0f, 100.0f))
            );
            ConfigEnableLootFovCulling = Config.Bind(
                "4. Loose Loot Culling",
                "EnableLootFovCulling",
                true,
                "Hides 3D renderers of loose loot when behind the player's back beyond 5 meters."
            );

            // 5. Anti-Shoot-Stutter
            ConfigDisableMuzzleShadows = Config.Bind(
                "5. Anti-Shoot-Stutter",
                "DisableMuzzleShadows",
                true,
                "Disables dynamic shadow maps for muzzle flash lights, fixing gunplay stutters."
            );
            ConfigCombatGCGuard = Config.Bind(
                "5. Anti-Shoot-Stutter",
                "CombatGCGuard",
                true,
                "Locks Unity Garbage Collector during gunfire so stop-the-world GC freeze spikes cannot happen during combat."
            );
            ConfigOptimizeShellCasings = Config.Bind(
                "5. Anti-Shoot-Stutter",
                "OptimizeShellCasings",
                true,
                "Automatically disables physics simulation on spent brass casings after 2.5 seconds to prevent PhysX collider bloat."
            );

            // 6. Unity Engine Tuning
            ConfigTunePhysics = Config.Bind(
                "6. Engine Tuning",
                "TunePhysics",
                true,
                "Reuses physics collision event memory (reuseCollisionCallbacks) and skips backface raycasts (queriesHitBackfaces) for faster ballistics & vision checks."
            );
            ConfigTuneAsyncUpload = Config.Bind(
                "6. Engine Tuning",
                "TuneAsyncUpload",
                true,
                "Increases async texture upload buffer to 128 MB and time slice to 4 ms, preventing hitching when turning corners."
            );
            ConfigTuneParticleBudget = Config.Bind(
                "6. Engine Tuning",
                "TuneParticleBudget",
                true,
                "Caps scene particle physics collision raycasts from default 4096 down to reasonable budget."
            );
            ConfigParticleRaycastLimit = Config.Bind(
                "6. Engine Tuning",
                "ParticleRaycastLimit",
                64,
                new ConfigDescription("Max particle collision raycasts per frame.", new AcceptableValueRange<int>(16, 512))
            );
            ConfigDisableRealtimeReflections = Config.Bind(
                "6. Engine Tuning",
                "DisableRealtimeReflections",
                true,
                "Disables expensive real-time reflection probe recalculations."
            );
            ConfigOptimizeSkinWeights = Config.Bind(
                "6. Engine Tuning",
                "OptimizeSkinWeights",
                true,
                "Sets TwoBones vertex skinning weights instead of FourBones, halving CPU skinning math on character models."
            );

            // 7. Draw Distance & LOD Bias
            ConfigClampViewDistance = Config.Bind(
                "7. Draw Distance & LOD",
                "ClampViewDistance",
                true,
                "Clamps camera far draw distance to prevent rendering geometry and trees 1.5km away behind the fog."
            );
            ConfigMaxViewDistance = Config.Bind(
                "7. Draw Distance & LOD",
                "MaxViewDistance",
                650.0f,
                new ConfigDescription("Maximum camera far clip plane in meters (default 650m vs vanilla 1500m).", new AcceptableValueRange<float>(300.0f, 1500.0f))
            );
            ConfigTuneLODBias = Config.Bind(
                "7. Draw Distance & LOD",
                "TuneLODBias",
                true,
                "Reduces polygon count of distant props by tuning LOD bias."
            );
            ConfigLODBiasValue = Config.Bind(
                "7. Draw Distance & LOD",
                "LODBiasValue",
                0.7f,
                new ConfigDescription("LOD bias multiplier (0.7 = transitions to simpler LODs sooner, hiding tiny distant details; 1.0 = standard, 2.0 = heavy high-poly).", new AcceptableValueRange<float>(0.3f, 2.5f))
            );

            // 8. Shadow & Rendering Tuning
            ConfigEnableShadowTuning = Config.Bind(
                "8. Shadows & Rendering",
                "EnableShadowTuning",
                true,
                "Enables fine-tuned shadow cascade and distance clamping."
            );
            ConfigDisableShadowsCompletely = Config.Bind(
                "8. Shadows & Rendering",
                "DisableShadowsCompletely",
                false,
                "Completely disables all shadow passes (ShadowQuality.Disable) for extreme FPS boost on weak hardware."
            );
            ConfigShadowCascades = Config.Bind(
                "8. Shadows & Rendering",
                "ShadowCascades",
                2,
                new ConfigDescription("Shadow cascades (1, 2, or 4). 2 cascades cuts shadow render passes by half vs vanilla 4.", new AcceptableValueList<int>(1, 2, 4))
            );
            ConfigMaxShadowDistance = Config.Bind(
                "8. Shadows & Rendering",
                "MaxShadowDistance",
                45.0f,
                new ConfigDescription("Clamps maximum shadow distance in meters. Default in EFT is 100m+.", new AcceptableValueRange<float>(15.0f, 150.0f))
            );
            ConfigShadowResolution = Config.Bind(
                "8. Shadows & Rendering",
                "ShadowResolution",
                1,
                new ConfigDescription("Shadow map resolution: 0 = Low (512), 1 = Medium (1024), 2 = High (2048).", new AcceptableValueList<int>(0, 1, 2))
            );

            // 9. Volumetric Lighting & Fog
            ConfigDisableVolumetricLighting = Config.Bind(
                "9. Volumetric & Fog",
                "DisableVolumetricLighting",
                true,
                "Disables heavy volumetric light rendering, blur shaders, and secondary command buffers across maps."
            );
            ConfigDisableVolumetricFog = Config.Bind(
                "9. Volumetric & Fog",
                "DisableVolumetricFog",
                true,
                "Bypasses compute shader volumetric fog injection and raymarching passes for massive GPU relief."
            );

            // 10. Optics & Scopes (Picture-in-Picture)
            ConfigOptimizeOptics = Config.Bind(
                "10. Optics & Scopes (PiP)",
                "OptimizeOptics",
                true,
                "Optimizes dual-camera Picture-in-Picture scopes by capping resolution and clamping view distance."
            );
            ConfigMaxOpticResolution = Config.Bind(
                "10. Optics & Scopes (PiP)",
                "MaxOpticResolution",
                512,
                new ConfigDescription("Caps internal optic render texture resolution (256, 384, 512, 768, 1024). 512 gives great clarity with 4x less fillrate cost than 1024.", new AcceptableValueList<int>(256, 384, 512, 768, 1024))
            );
            ConfigOpticMaxDistance = Config.Bind(
                "10. Optics & Scopes (PiP)",
                "OpticMaxDistance",
                400.0f,
                new ConfigDescription("Maximum far clip plane for optic camera. Prevents rendering distant LODs and geometry beyond scope usefulness.", new AcceptableValueRange<float>(150.0f, 1000.0f))
            );

            // 11. AI Bot CPU Optimization (Option A - Zero SAIN/ORBIT conflict)
            ConfigEnableAITickThrottling = Config.Bind(
                "11. AI Bot CPU Optimization",
                "EnableAITickThrottling",
                true,
                "Throttles distant non-combat bot decision ticks to save 25-35% CPU. Instantly returns to 100% native tick rate in combat."
            );
            ConfigAIThrottleDistance = Config.Bind(
                "11. AI Bot CPU Optimization",
                "AIThrottleDistance",
                80.0f,
                new ConfigDescription("Distance (meters) beyond which non-combat bots have their AI decision ticks throttled.", new AcceptableValueRange<float>(40f, 200f))
            );
            ConfigAITickRateSkip = Config.Bind(
                "11. AI Bot CPU Optimization",
                "AITickRateSkip",
                3,
                new ConfigDescription("Tick skip multiplier for distant bots (e.g. 3 = ticks 1 out of 3 frames, ~20-25 Hz).", new AcceptableValueRange<int>(2, 6))
            );
            ConfigEnableIKCulling = Config.Bind(
                "11. AI Bot CPU Optimization",
                "EnableIKCulling",
                true,
                "Bypasses Full Body Biped Inverse Kinematics (FBBIK) on distant bots to save massive CPU overhead."
            );
            ConfigIKCullDistance = Config.Bind(
                "11. AI Bot CPU Optimization",
                "IKCullDistance",
                45.0f,
                new ConfigDescription("Distance (meters) beyond which bot Full Body Biped IK is bypassed.", new AcceptableValueRange<float>(20f, 150f))
            );
            ConfigEnableAnimatorCulling = Config.Bind(
                "11. AI Bot CPU Optimization",
                "EnableAnimatorCulling",
                true,
                "Enables CullUpdateTransforms on bot animators so Unity skips updating bone hierarchies when occluded/offscreen."
            );

            // 12. Weapon & Inventory Throttling
            ConfigEnableItemTakerThrottling = Config.Bind(
                "12. Weapon & Inventory Throttling",
                "EnableItemTakerThrottling",
                true,
                "Throttles bot loose loot query frequency to eliminate frame drops caused by spatial searches."
            );
            ConfigCombatSuppressItemTaking = Config.Bind(
                "12. Weapon & Inventory Throttling",
                "CombatSuppressItemTaking",
                true,
                "Completely halts bot loot searching while in combat (when enemy is detected)."
            );
            ConfigItemTakerInterval = Config.Bind(
                "12. Weapon & Inventory Throttling",
                "ItemTakerInterval",
                2.5f,
                new ConfigDescription("Minimum interval in seconds between loose loot searches per bot.", new AcceptableValueRange<float>(0.5f, 10.0f))
            );
            ConfigItemTakerMaxDistance = Config.Bind(
                "12. Weapon & Inventory Throttling",
                "ItemTakerMaxDistance",
                65.0f,
                new ConfigDescription("Distance beyond which bots completely skip loose loot searches.", new AcceptableValueRange<float>(20.0f, 200.0f))
            );

            // 13. Heap & Working Set Management
            ConfigTrimWorkingSetOnRaidEnd = Config.Bind(
                "13. Heap & Working Set",
                "TrimWorkingSetOnRaidEnd",
                true,
                "Frees unreferenced memory pages back to Windows OS when leaving raid to prevent RAM bloating over long sessions."
            );

            // 14. Terrain & Foliage (Lighthouse / Woods / Shoreline)
            ConfigOptimizeTerrain = Config.Bind(
                "14. Terrain & Foliage",
                "OptimizeTerrain",
                true,
                "Enables GPU instancing for grass, caps grass render distance, and simplifies distant terrain meshes."
            );
            ConfigDetailDistance = Config.Bind(
                "14. Terrain & Foliage",
                "DetailDistance",
                45.0f,
                new ConfigDescription("Grass and foliage draw distance in meters (vanilla is 100m+).", new AcceptableValueRange<float>(15.0f, 150.0f))
            );
            ConfigDetailDensity = Config.Bind(
                "14. Terrain & Foliage",
                "DetailDensity",
                0.6f,
                new ConfigDescription("Grass and foliage density multiplier.", new AcceptableValueRange<float>(0.1f, 1.0f))
            );
            ConfigHeightmapPixelError = Config.Bind(
                "14. Terrain & Foliage",
                "HeightmapPixelError",
                5.0f,
                new ConfigDescription("Terrain geometry simplification error tolerance (higher = fewer vertices, vanilla is 1-2).", new AcceptableValueRange<float>(1.0f, 20.0f))
            );
            ConfigTreeDistance = Config.Bind(
                "14. Terrain & Foliage",
                "TreeDistance",
                350.0f,
                new ConfigDescription("Distance to render 3D trees before switching to billboards (vanilla is 1000m+).", new AcceptableValueRange<float>(100.0f, 1500.0f))
            );
            ConfigTreeBillboardDistance = Config.Bind(
                "14. Terrain & Foliage",
                "TreeBillboardDistance",
                100.0f,
                new ConfigDescription("Distance for tree billboard transitions.", new AcceptableValueRange<float>(50.0f, 500.0f))
            );
            ConfigTreeFullLODCount = Config.Bind(
                "14. Terrain & Foliage",
                "TreeFullLODCount",
                40,
                new ConfigDescription("Maximum number of trees rendered at full LOD detail.", new AcceptableValueRange<int>(10, 300))
            );

            // 15. RAM & Memory Management (Options 1 & 3)
            ConfigEnablePostLoadPurge = Config.Bind(
                "15. RAM Management",
                "EnablePostLoadPurge",
                true,
                "Option 1: Flushes temporary AssetBundle caches and unreferenced memory immediately after loading into raid (saves 2-3 GB RAM)."
            );
            ConfigEnableAudioPurge = Config.Bind(
                "15. RAM Management",
                "EnableAudioPurge",
                true,
                "Option 3: Unloads long non-combat audio clips (menu music, hideout ambient) from RAM during raid (saves 1-2 GB RAM)."
            );

            // Listen for config changes in F12 menu
            Config.SettingChanged += (sender, args) => EngineTuner.ApplySettings();
        }
    }
}
