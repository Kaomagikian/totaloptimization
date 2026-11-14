using System;
using System.Collections.Generic;
using UnityEngine;
using EFT;

namespace TotalOptimization.Components
{
    /// <summary>
    /// Lightweight component attached strictly to AI bots to configure
    /// Animator culling mode (CullUpdateTransforms).
    /// NEVER touches human player and NEVER touches weapon procedural animations.
    /// </summary>
    public class BotOptimizerComponent : MonoBehaviour
    {
        public static readonly List<BotOptimizerComponent> ActiveBots = new List<BotOptimizerComponent>();

        private Player _botPlayer;
        private Animator _animator;
        private bool _animatorConfigured = false;

        public bool IsCulled => false;

        private void Awake()
        {
            _botPlayer = GetComponent<Player>();

            // ABSOLUTE SAFETY GUARD: Never ever attach to or optimize the human player!
            if (_botPlayer == null || _botPlayer.IsYourPlayer)
            {
                Destroy(this);
                return;
            }

            ActiveBots.Add(this);
        }

        private void Start()
        {
            if (_botPlayer == null || _botPlayer.IsYourPlayer)
            {
                RestoreVanilla();
                Destroy(this);
                return;
            }

            ConfigureAnimator();
        }

        public void ConfigureAnimator()
        {
            if (_animatorConfigured || _botPlayer == null || _botPlayer.IsYourPlayer) return;

            if (_animator == null)
            {
                _animator = _botPlayer.GetComponentInChildren<Animator>();
            }

            if (_animator != null)
            {
                if (Plugin.ConfigEnabled.Value && Plugin.ConfigEnableAnimatorCulling.Value)
                {
                    _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
                else
                {
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                _animatorConfigured = true;
            }
        }

        public void RestoreVanilla()
        {
            if (_animator != null)
            {
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        public static void RestoreAllBots()
        {
            for (int i = 0; i < ActiveBots.Count; i++)
            {
                var bot = ActiveBots[i];
                if (bot != null)
                {
                    bot.RestoreVanilla();
                }
            }
        }

        private void OnDestroy()
        {
            ActiveBots.Remove(this);
        }
    }
}
