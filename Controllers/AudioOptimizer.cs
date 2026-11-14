using System;
using UnityEngine;

namespace TotalOptimization.Controllers
{
    public static class AudioOptimizer
    {
        public static void PurgeUnusedAudio()
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableAudioPurge.Value)
                return;

            try
            {
                AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                if (allClips == null) return;

                int purgedCount = 0;
                for (int i = 0; i < allClips.Length; i++)
                {
                    AudioClip clip = allClips[i];
                    if (clip == null) continue;

                    // Unload long clips (menu music, hideout ambient, trader background audio)
                    // Short combat SFX (< 5s: gunfire, reloads, footsteps) remain instantly accessible in RAM!
                    if (clip.length > 5.0f && clip.loadType != AudioClipLoadType.Streaming)
                    {
                        clip.UnloadAudioData();
                        purgedCount++;
                    }
                }

                if (Plugin.ConfigDebugLogging.Value && purgedCount > 0)
                {
                    Plugin.Log.LogInfo($"[Total Optimization] Audio Purge: Unloaded {purgedCount} long music/ambient clips from RAM.");
                }
            }
            catch (Exception ex)
            {
                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogError($"[Total Optimization] Audio Purge error: {ex.Message}");
            }
        }
    }
}
