using System;
using System.Collections.Generic;
using UnityEngine;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using TotalOptimization.Patches;

namespace TotalOptimization.Controllers
{
    public class LootCuller : MonoBehaviour
    {
        private float _nextCheckTime;
        private bool _isScanned = false;
        private readonly List<LootItemData> _lootItems = new List<LootItemData>(1000);

        private class LootItemData
        {
            public LootItem Item;
            public Renderer[] Renderers;
            public Vector3 Position;
            public bool IsVisible;
        }

        private void Start()
        {
            _nextCheckTime = Time.time + 3.0f;
        }

        public void ResetState()
        {
            _isScanned = false;
            _lootItems.Clear();
        }

        public void RestoreAll()
        {
            for (int i = 0; i < _lootItems.Count; i++)
            {
                var data = _lootItems[i];
                if (data?.Renderers != null)
                {
                    for (int r = 0; r < data.Renderers.Length; r++)
                    {
                        if (data.Renderers[r] != null)
                            data.Renderers[r].enabled = true;
                    }
                }
            }
        }

        private void Update()
        {
            if (!Plugin.ConfigEnabled.Value || !Plugin.ConfigEnableLootCulling.Value) return;

            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + 1.2f;

            if (!Singleton<GameWorld>.Instantiated)
            {
                if (_isScanned)
                {
                    ResetState();
                }
                return;
            }

            Player mainPlayer = GameWorldRegisterPatch.MainPlayer;
            if (mainPlayer == null) return;

            // Scan loot items only once after GameWorld has spawned player
            if (!_isScanned)
            {
                ScanLootItems();
            }

            if (_lootItems.Count == 0) return;

            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : mainPlayer.Position;
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.zero;
            bool checkFov = cam != null && Plugin.ConfigEnableLootFovCulling.Value;

            float maxDistSqr = Plugin.ConfigLootCullDistance.Value * Plugin.ConfigLootCullDistance.Value;

            for (int i = _lootItems.Count - 1; i >= 0; i--)
            {
                LootItemData data = _lootItems[i];
                if (data.Item == null || data.Item.transform.IsChildOf(mainPlayer.gameObject.transform) || data.Item.GetComponentInParent<Player>() != null)
                {
                    // Item was picked up or attached to player: ensure renderers are visible and un-track
                    if (data?.Renderers != null)
                    {
                        for (int r = 0; r < data.Renderers.Length; r++)
                        {
                            if (data.Renderers[r] != null)
                                data.Renderers[r].enabled = true;
                        }
                    }
                    _lootItems.RemoveAt(i);
                    continue;
                }

                Vector3 toItem = data.Position - camPos;
                float distSqr = toItem.sqrMagnitude;
                bool shouldBeVisible = distSqr <= maxDistSqr;

                // GTA-Style Behind-The-Back Culling for loose loot beyond 5 meters
                if (shouldBeVisible && checkFov && distSqr > 25f)
                {
                    if (Vector3.Dot(camFwd, toItem.normalized) < 0f)
                    {
                        shouldBeVisible = false;
                    }
                }

                if (shouldBeVisible != data.IsVisible)
                {
                    data.IsVisible = shouldBeVisible;
                    if (data.Renderers != null)
                    {
                        for (int r = 0; r < data.Renderers.Length; r++)
                        {
                            if (data.Renderers[r] != null)
                            {
                                data.Renderers[r].enabled = shouldBeVisible;
                            }
                        }
                    }
                }
            }
        }

        public void ScanLootItems()
        {
            _isScanned = true;
            _lootItems.Clear();

            try
            {
                LootItem[] items = UnityEngine.Object.FindObjectsOfType<LootItem>();
                if (items == null) return;

                Transform playerTransform = GameWorldRegisterPatch.HumanPlayer != null ? GameWorldRegisterPatch.HumanPlayer.gameObject.transform : null;
                Transform camTransform = Camera.main != null ? Camera.main.transform : null;

                for (int i = 0; i < items.Length; i++)
                {
                    LootItem item = items[i];
                    if (item == null) continue;

                    // Absolute safety: never cull loot held by, worn by, or attached to any player or camera
                    Transform itemT = item.transform;
                    if (playerTransform != null && itemT.IsChildOf(playerTransform)) continue;
                    if (camTransform != null && itemT.IsChildOf(camTransform)) continue;
                    if (item.GetComponentInParent<Player>() != null) continue;

                    Renderer[] rnds = item.GetComponentsInChildren<Renderer>(true);
                    _lootItems.Add(new LootItemData
                    {
                        Item = item,
                        Renderers = rnds,
                        Position = item.transform.position,
                        IsVisible = true
                    });
                }

                if (Plugin.ConfigDebugLogging.Value && _lootItems.Count > 0)
                {
                    Plugin.Log.LogInfo($"[Total Optimization] Loot Culler tracked {_lootItems.Count} loose items on map.");
                }
            }
            catch (Exception ex)
            {
                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogError($"[Total Optimization] Error scanning loot items: {ex.Message}");
            }
        }
    }
}
