using System;
using UnityEngine;

namespace TotalOptimization.Controllers
{
    public static class TextureClampController
    {
        public static bool HasClampedThisRaid { get; set; } = false;

        public static void ResetState()
        {
            HasClampedThisRaid = false;
        }

        public static void ClampAllTextures()
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableTextureClamping.Value)
            {
                return;
            }

            if (HasClampedThisRaid) return;
            HasClampedThisRaid = true;

            try
            {
                // Discards unused high-res mipmaps from GPU RAM safely via Unity's native streaming engine
                Texture.streamingTextureDiscardUnusedMips = true;
            }
            catch (Exception ex)
            {
                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogError($"[Total Optimization] Texture clamp error: {ex.Message}");
            }
        }
    }
}
