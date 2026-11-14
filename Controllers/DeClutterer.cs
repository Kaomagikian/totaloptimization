using System;
using System.Collections.Generic;
using UnityEngine;
using EFT;
using EFT.Interactive;
using TotalOptimization.Patches;

namespace TotalOptimization.Controllers
{
    public static class DeClutterer
    {
        private static readonly HashSet<string> _clutterKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "puddle", "puddles", "decal_puddle",
            "debris_pile",
            "trash_pile", "trash_can", "trash_bin",
            "garbage_pile",
            "cigarette", "cigarettes", "cigar", "sigaret",
            "rubbish",
            "tin_can", "can_crushed", "can_tin",
            "broken_glass", "glass_pieces", "glass_shards",
            "splinter", "splinters",
            "cardboard_box_damaged", "paper_scrap", "paper_trash",
            "dirt_pile", "leaves_pile", "autumn_leaves",
            "broken_bottle", "plastic_bottle", "crushed_can"
        };

        // Protected keywords that must NEVER be culled
        private static readonly string[] _protectedKeywords = new string[]
        {
            "floor", "wall", "ceiling", "ground", "terrain", "road", "stairs", "stair",
            "ladder", "roof", "fence", "door", "window", "barrier", "key", "lamp", "light",
            "switch", "extract", "camera", "player", "weapon", "container", "item", "mag",
            "scope", "sight", "silencer", "muzzle", "barrel", "patron", "ammo", "mod_",
            "gun", "rifle", "pistol", "shotgun", "receiver", "stock", "grip", "mount",
            "suppressor", "bullet", "hand", "arm", "anim", "body", "cloth", "rig", "vest", "armor", "helmet"
        };

        public static int CulledObjectCount { get; private set; }

        public static void RunDeClutter()
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableDeClutterer.Value)
            {
                return;
            }

            try
            {
                float start = Time.realtimeSinceStartup;
                int culled = 0;

                MeshRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
                if (renderers == null) return;

                int playerLayer = LayerMask.NameToLayer("Player");
                int handsLayer = LayerMask.NameToLayer("Hands");
                int weaponLayer = LayerMask.NameToLayer("Weapon");
                int hp1Layer = LayerMask.NameToLayer("HighPoly1");
                int hp2Layer = LayerMask.NameToLayer("HighPoly2");

                Camera mainCam = Camera.main;
                Transform camTransform = mainCam != null ? mainCam.transform : null;
                Player humanPlayer = GameWorldRegisterPatch.HumanPlayer;
                Transform playerTransform = humanPlayer != null ? humanPlayer.gameObject.transform : null;

                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer r = renderers[i];
                    if (r == null || !r.enabled) continue;

                    GameObject go = r.gameObject;
                    if (go == null) continue;

                    // ABSOLUTE RULE 1: Only baked static map geometry!
                    // Weapons, attachments, players, and loot are ALWAYS dynamic (isStatic == false).
                    if (!go.isStatic) continue;

                    // ABSOLUTE RULE 2: Layer protection
                    int layer = go.layer;
                    if (layer == playerLayer || layer == handsLayer || layer == weaponLayer ||
                        layer == hp1Layer || layer == hp2Layer)
                    {
                        continue;
                    }

                    // ABSOLUTE RULE 3: Never cull anything attached to the camera or player
                    Transform t = r.transform;
                    if (camTransform != null && t.IsChildOf(camTransform)) continue;
                    if (playerTransform != null && t.IsChildOf(playerTransform)) continue;

                    // ABSOLUTE RULE 4: Hierarchy checks
                    if (r.GetComponentInParent<LootItem>() != null) continue;
                    if (r.GetComponentInParent<LootableContainer>() != null) continue;
                    if (r.GetComponentInParent<Player>() != null) continue;
                    if (r.GetComponentInParent<Door>() != null) continue;
                    if (r.GetComponentInParent<ExfiltrationPoint>() != null) continue;
                    if (r.GetComponentInParent<WorldInteractiveObject>() != null) continue;

                    string name = go.name;

                    // Guard protected keywords
                    bool isProtected = false;
                    for (int p = 0; p < _protectedKeywords.Length; p++)
                    {
                        if (name.IndexOf(_protectedKeywords[p], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isProtected = true;
                            break;
                        }
                    }
                    if (isProtected) continue;

                    // ONLY cull objects matching verified clutter keywords
                    bool isClutter = false;
                    foreach (string keyword in _clutterKeywords)
                    {
                        if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isClutter = true;
                            break;
                        }
                    }

                    if (isClutter)
                    {
                        r.enabled = false;
                        culled++;
                    }
                }

                CulledObjectCount = culled;
                float elapsed = (Time.realtimeSinceStartup - start) * 1000f;

                if (Plugin.ConfigDebugLogging.Value)
                {
                    Plugin.Log.LogInfo($"[Total Optimization] De-Clutterer safely culled {culled} static clutter objects in {elapsed:F1}ms.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Total Optimization] De-Clutterer error: {ex.Message}");
            }
        }
    }
}
