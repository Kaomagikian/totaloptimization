using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using EFT;
using Comfort.Common;
using TotalOptimization.Components;

namespace TotalOptimization.Patches
{
    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.RegisterPlayer))]
    public static class GameWorldRegisterPatch
    {
        private static readonly FieldInfo _animatorField = AccessTools.Field(typeof(Player), "_createdAnimator");
        public static Player MainPlayer { get; private set; }

        public static void ResetState()
        {
            MainPlayer = null;
        }

        public static Player HumanPlayer
        {
            get
            {
                if (MainPlayer != null && MainPlayer.IsYourPlayer) return MainPlayer;
                if (Singleton<GameWorld>.Instantiated)
                {
                    var gw = Singleton<GameWorld>.Instance;
                    if (gw.MainPlayer != null)
                    {
                        MainPlayer = gw.MainPlayer;
                        return MainPlayer;
                    }
                    if (gw.AllAlivePlayersList != null)
                    {
                        for (int i = 0; i < gw.AllAlivePlayersList.Count; i++)
                        {
                            var p = gw.AllAlivePlayersList[i];
                            if (p != null && p.IsYourPlayer)
                            {
                                MainPlayer = p;
                                return MainPlayer;
                            }
                        }
                    }
                }
                return MainPlayer;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(IPlayer iPlayer)
        {
            if (iPlayer == null) return;

            Player player = iPlayer as Player;
            if (player == null) return;

            // Absolute check for the human player: IsYourPlayer is natively true ONLY for the local human!
            if (player.IsYourPlayer)
            {
                MainPlayer = player;

                // CRITICAL FIX: Ensure the human player's own first-person arms and weapon animators
                // NEVER get culled under any circumstance!
                try
                {
                    Animator[] animators = player.GetComponentsInChildren<Animator>(true);
                    for (int i = 0; i < animators.Length; i++)
                    {
                        if (animators[i] != null)
                        {
                            animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        }
                    }

                    if (player.ProceduralWeaponAnimation != null)
                    {
                        player.ProceduralWeaponAnimation.enabled = true;
                    }

                    BotOptimizerComponent existingOpt = player.GetComponent<BotOptimizerComponent>();
                    if (existingOpt != null)
                    {
                        UnityEngine.Object.Destroy(existingOpt);
                    }

                    // Attach Weapon Sentinel to human player
                    if (player.GetComponent<PlayerWeaponSentinel>() == null)
                    {
                        player.gameObject.AddComponent<PlayerWeaponSentinel>();
                    }
                }
                catch { }

                // Trigger raid start optimization routines safely ONCE per raid
                Plugin.Instance?.TriggerRaidStartRoutines();

                if (Plugin.ConfigDebugLogging.Value)
                    Plugin.Log.LogInfo($"[TotalOptimization] Registered MainPlayer: {player.Profile?.Nickname}");
                return;
            }

            // It is an AI BOT
            if (!Plugin.ConfigEnabled.Value) return;

            // 1. Animator Culling (Bot only)
            if (Plugin.ConfigEnableAnimatorCulling.Value)
            {
                try
                {
                    Animator animator = _animatorField?.GetValue(player) as Animator;
                    if (animator == null)
                    {
                        animator = player.GetComponentInChildren<Animator>();
                    }

                    if (animator != null)
                    {
                        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    }
                }
                catch (Exception ex)
                {
                    if (Plugin.ConfigDebugLogging.Value)
                        Plugin.Log.LogError($"[TotalOptimization] Failed to configure Animator culling: {ex.Message}");
                }
            }

            // 2. Attach BotOptimizerComponent to configure Animator and track AI bot
            if (player.gameObject.GetComponent<BotOptimizerComponent>() == null)
            {
                player.gameObject.AddComponent<BotOptimizerComponent>();
            }
        }
    }
}
