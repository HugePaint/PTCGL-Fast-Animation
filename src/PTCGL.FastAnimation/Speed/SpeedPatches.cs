using System.Reflection;
using PTCGL.FastAnimation.Speed;
using UnityEngine;

namespace PTCGL.FastAnimation.Speed
{
    internal static class BoosterPatches
    {
        public static void ScaleOpeningDurations(object __instance)
        {
            var enabled = Plugin.Cfg.SpeedBoosterCards.Value;
            var instant = enabled && Playback.Skip;
            var multiplier = enabled ? Playback.Multiplier : 1f;
            DurationScale.Apply(__instance, "cardSlideInDuration", multiplier, instant);
            DurationScale.Apply(__instance, "cardScaleInAnimDuration", multiplier, instant);
        }

        public static void RevealStarted(object __instance)
        {
            if (!Plugin.Cfg.SpeedBoosterCards.Value)
                return;

            var animator = Playback.Field<Animator>(__instance, "anim");
            if (animator == null)
                return;

            if (!Playback.Skip)
            {
                animator.speed = Playback.Multiplier;
                return;
            }

            Playback.HoldTriggeredEnd(animator);
            ShowNewCardMarker(__instance);
            Playback.Invoke(__instance, "OnRevealStart");
            Playback.Invoke(__instance, "OnRevealComplete");
            animator.speed = 0f;
        }

        public static void RevealFinished(object __instance)
        {
            if (!Plugin.Cfg.SpeedBoosterCards.Value || Playback.Skip)
                return;
            Playback.SetAnimatorSpeed(__instance, "anim", 1f);
        }

        public static void OutroTriggered(object __instance)
        {
            if (!Plugin.Cfg.SpeedBoosterCards.Value)
                return;

            var animator = Playback.Field<Animator>(__instance, "anim");
            if (animator == null)
                return;

            if (Playback.Skip)
            {
                Playback.HoldTriggeredEnd(animator);
                Playback.Invoke(__instance, "OnOutroAnimationComplete");
                return;
            }

            animator.speed *= Playback.Multiplier;
        }

        public static void OutroAfterReveal(object __instance)
        {
            if (!Plugin.Cfg.SpeedBoosterCards.Value)
                return;
            if (Playback.FieldInt(__instance, "<>1__state") != -1)
                return;

            var card = Playback.Field<object>(__instance, "<>4__this");
            if (card == null || Playback.PropertyBool(card, "IsRevealing"))
                return;

            if (Playback.Skip)
            {
                var animator = Playback.Field<Animator>(card, "anim");
                if (animator != null)
                    Playback.HoldTriggeredEnd(animator);
                Playback.Invoke(card, "OnOutroAnimationComplete");
                return;
            }

            Playback.SetAnimatorSpeed(card, "anim", Playback.Multiplier);
        }

        public static void ParticleOutro(object __instance)
        {
            if (!Plugin.Cfg.SpeedBoosterCards.Value)
                return;

            var holder = Playback.Field<Component>(__instance, "effectsHolder");
            if (holder == null)
                return;

            var systems = holder.GetComponentsInChildren<ParticleSystem>(false);
            if (Playback.Skip)
            {
                for (var i = 0; i < systems.Length; i++)
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            var multiplier = Playback.Multiplier;
            for (var i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.simulationSpeed *= multiplier;
            }
        }

        private static void ShowNewCardMarker(object card)
        {
            var canvas = Playback.Field<Component>(card, "newCardCanvas");
            if (canvas == null)
                return;
            var field = card.GetType().GetField("_isNewToPlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var isNew = field?.GetValue(card) is bool value && value;
            canvas.gameObject.SetActive(isNew);
        }
    }

    internal static class RewardPatches
    {
        public static void ScaleIntro(object __instance)
        {
            var enabled = Plugin.Cfg.SpeedRewardPopups.Value;
            DurationScale.Apply(
                __instance,
                "introAnimationDuration",
                enabled ? Playback.Multiplier : 1f,
                enabled && Playback.Skip);
        }
    }

    internal static class CurrencyPatches
    {
        public static void ScaleFlight(object __instance)
        {
            if (__instance is not Component component || !IsCurrencyFlight(component))
                return;

            var enabled = Plugin.Cfg.SpeedCurrencyFlight.Value;
            var instant = enabled && Playback.Skip;
            var multiplier = enabled ? Playback.Multiplier : 1f;
            DurationScale.Apply(__instance, "Duration", multiplier, instant);
            DurationScale.Apply(__instance, "Delay", multiplier, instant);
        }

        public static void FinishFlight(object __instance)
        {
            if (!Playback.Skip || !Plugin.Cfg.SpeedCurrencyFlight.Value)
                return;
            if (__instance is not Component component || !IsCurrencyFlight(component))
                return;

            Playback.CompleteTween(__instance);
        }

        private static bool IsCurrencyFlight(Component component)
        {
            var transform = component.transform;
            for (var depth = 0; transform != null && depth < 8; depth++)
            {
                if (transform.name.IndexOf("Currency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                transform = transform.parent;
            }

            return false;
        }
    }

    internal static class MissionPatches
    {
        public static void Checkmark(object __instance, string animationState)
        {
            var completing = Playback.FieldString(__instance, "checkmarkCompletingState");
            var isCompleting = animationState == completing;
            if (Plugin.Cfg.SpeedMissionRewards.Value && Playback.Skip && isCompleting)
            {
                Playback.SetAnimatorSpeed(__instance, "checkmarkAnimator", 0f);
                return;
            }

            var speed = Plugin.Cfg.SpeedMissionRewards.Value
                && isCompleting
                && Playback.Multiplier > 1f
                    ? Playback.Multiplier
                    : 1f;
            Playback.SetAnimatorSpeed(__instance, "checkmarkAnimator", speed);
        }

        public static void CheckmarkSkipped(object __instance, string animationState)
        {
            if (!Plugin.Cfg.SpeedMissionRewards.Value || !Playback.Skip)
                return;
            if (animationState != Playback.FieldString(__instance, "checkmarkCompletingState"))
                return;

            var animator = Playback.Field<Animator>(__instance, "checkmarkAnimator");
            if (animator == null || !animator.gameObject.activeInHierarchy)
                return;

            animator.Play(animationState, 0, 1f);
            animator.speed = 0f;
            Playback.Invoke(__instance, "OnCheckmarkAnimationComplete");
        }

        public static void CheckmarkFinished(object __instance)
        {
            if (Playback.Skip)
                return;
            Playback.SetAnimatorSpeed(__instance, "checkmarkAnimator", 1f);
        }

        public static bool SetCompleteStarted(object __instance)
        {
            if (!Plugin.Cfg.SpeedMissionRewards.Value)
                return true;

            if (Playback.Skip)
            {
                var controller = Playback.Field<Component>(__instance, "cardDexSetCompleteController");
                if (controller != null)
                {
                    Playback.Invoke(controller, "AnimationComplete");
                    return false;
                }
            }

            if (Playback.Multiplier <= 1f)
                return true;

            var animator = Playback.Field<Animator>(__instance, "animator");
            var boost = TreeSpeedBoost.Attach(animator);
            if (boost != null)
                boost.Speed = Playback.Multiplier;
            return true;
        }

        public static void SetCompleteFinished(object __instance)
        {
            var listener = Playback.Field<Component>(__instance, "cardDexSetCompleteAnimListener");
            var animator = Playback.Field<Animator>(listener, "animator");
            TreeSpeedBoost.Stop(animator);
        }
    }

    /// <summary>
    /// Keeps every Animator under the set-complete sequence at the claim multiplier,
    /// including objects the sequence enables after it starts. Removed when the sequence ends.
    /// </summary>
    internal sealed class TreeSpeedBoost : MonoBehaviour
    {
        public float Speed = 1f;

        public static TreeSpeedBoost? Attach(Animator? root)
        {
            if (root == null)
                return null;
            var existing = root.GetComponent<TreeSpeedBoost>();
            if (existing != null)
                return existing;
            return root.gameObject.AddComponent<TreeSpeedBoost>();
        }

        public static void Stop(Animator? root)
        {
            if (root == null)
                return;
            var boost = root.GetComponent<TreeSpeedBoost>();
            if (boost != null)
                boost.ResetAndRemove();
            else
                Playback.SetTreeSpeed(root, 1f);
        }

        private void LateUpdate()
        {
            if (!Plugin.Cfg.SpeedMissionRewards.Value || Speed <= 1f)
            {
                ResetAndRemove();
                return;
            }

            Playback.SetTreeSpeed(GetComponent<Animator>(), Speed);
        }

        private void ResetAndRemove()
        {
            Playback.SetTreeSpeed(GetComponent<Animator>(), 1f);
            Destroy(this);
        }
    }
}
