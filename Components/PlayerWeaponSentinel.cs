using System;
using System.Collections.Generic;
using UnityEngine;
using EFT;

namespace TotalOptimization.Components
{
    /// <summary>
    /// Permanent sentinel attached to the human player.
    /// Guarantees that the player's first-person hands, weapons, attachments,
    /// and magazines are NEVER rendered invisible or culled by:
    /// 1. Smoothie HandsCulling (forceRenderingOff = false)
    /// 2. Unity frustum culling during sprint / stance transitions (updateWhenOffscreen = true)
    /// 3. LODGroup culling (ForceLOD(0))
    /// 4. Animator culling (AlwaysAnimate)
    /// </summary>
    public class PlayerWeaponSentinel : MonoBehaviour
    {
        private Player _player;
        private GameObject _lastHandsObject;
        private readonly List<Renderer> _weaponRenderers = new List<Renderer>(64);
        private readonly List<SkinnedMeshRenderer> _skinnedRenderers = new List<SkinnedMeshRenderer>(16);
        private readonly List<LODGroup> _lodGroups = new List<LODGroup>(16);
        private float _nextFullScanTime;

        private void Awake()
        {
            _player = GetComponent<Player>();
            if (_player == null || !_player.IsYourPlayer)
            {
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            ScanAndProtectWeapon();
        }

        private void LateUpdate()
        {
            if (_player == null) return;

            // Detect weapon switch or new attachment
            GameObject currentHands = GetHandsObject();
            if (currentHands != _lastHandsObject || Time.time > _nextFullScanTime)
            {
                ScanAndProtectWeapon();
            }

            // Rapid check every frame: guarantee all weapon renderers are visible and not forced off
            for (int i = 0; i < _weaponRenderers.Count; i++)
            {
                Renderer r = _weaponRenderers[i];
                if (r == null) continue;

                if (r.forceRenderingOff)
                {
                    r.forceRenderingOff = false;
                }

                if (!r.enabled)
                {
                    r.enabled = true;
                }
            }
        }

        private GameObject GetHandsObject()
        {
            try
            {
                if (_player != null && _player.HandsController != null)
                {
                    return _player.HandsController.ControllerGameObject;
                }
            }
            catch { }
            return null;
        }

        public void ScanAndProtectWeapon()
        {
            _nextFullScanTime = Time.time + 1.0f;
            _weaponRenderers.Clear();
            _skinnedRenderers.Clear();
            _lodGroups.Clear();

            GameObject hands = GetHandsObject();
            _lastHandsObject = hands;

            // 1. Protect Procedural Weapon Animation
            try
            {
                if (_player.ProceduralWeaponAnimation != null && !_player.ProceduralWeaponAnimation.enabled)
                {
                    _player.ProceduralWeaponAnimation.enabled = true;
                }
            }
            catch { }

            // 2. Protect Arm Animators
            try
            {
                Animator[] animators = _player.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    if (animators[i] != null)
                    {
                        animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    }
                }
            }
            catch { }

            if (hands == null) return;

            // 3. Scan and protect all weapon and attachment renderers
            try
            {
                hands.GetComponentsInChildren<Renderer>(true, _weaponRenderers);
                for (int i = 0; i < _weaponRenderers.Count; i++)
                {
                    Renderer r = _weaponRenderers[i];
                    if (r == null) continue;

                    r.forceRenderingOff = false;
                    r.enabled = true;

                    if (r is SkinnedMeshRenderer smr)
                    {
                        // Critical for sprint & procedural animations: never cull when bounding box shifts
                        smr.updateWhenOffscreen = true;
                        _skinnedRenderers.Add(smr);
                    }
                }

                // 4. Force LOD 0 on weapon parts so tiny sights/accessories never disappear
                hands.GetComponentsInChildren<LODGroup>(true, _lodGroups);
                for (int i = 0; i < _lodGroups.Count; i++)
                {
                    LODGroup lod = _lodGroups[i];
                    if (lod != null)
                    {
                        lod.ForceLOD(0);
                    }
                }
            }
            catch { }
        }

        private void OnDestroy()
        {
            _weaponRenderers.Clear();
            _skinnedRenderers.Clear();
            _lodGroups.Clear();
        }
    }
}
