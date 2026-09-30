using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PTCGL.FastAnimation.Speed
{
    internal static class Playback
    {
        public const float MaxMultiplier = 8f;

        public static float Multiplier
        {
            get
            {
                var raw = Plugin.Cfg.SpeedMultiplier.Value;
                if (raw < 1f)
                    return 1f;
                if (raw > MaxMultiplier)
                    return MaxMultiplier;
                return raw;
            }
        }

        public static bool Skip => Plugin.Cfg.SkipAnimations.Value;

        public static void Invoke(object? host, string methodName)
        {
            if (host == null)
                return;
            var method = host.GetType().GetMethod(methodName, InstanceFlags);
            method?.Invoke(host, null);
        }

        public static void HoldTriggeredEnd(Animator animator)
        {
            animator.speed = 1f;
            animator.Update(0f);
            if (!animator.IsInTransition(0))
                animator.Update(0.02f);

            var hash = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).fullPathHash
                : animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            animator.Play(hash, 0, 1f);
            animator.speed = 0f;
        }

        public static void CompleteTween(object? host)
        {
            var tweener = Field<object>(host, "_tweener");
            if (tweener == null)
                return;

            var methods = tweener.GetType().GetMethods(InstanceFlags);
            for (var i = 0; i < methods.Length; i++)
            {
                var parameters = methods[i].GetParameters();
                if (methods[i].Name != "Complete" || parameters.Length != 1 || parameters[0].ParameterType != typeof(bool))
                    continue;
                methods[i].Invoke(tweener, new object[] { true });
                return;
            }
        }

        public static void SetAnimatorSpeed(object? host, string fieldName, float speed)
        {
            var animator = Field<Animator>(host, fieldName);
            if (animator != null)
                animator.speed = speed;
        }

        public static void SetTreeSpeed(Animator? root, float speed)
        {
            if (root == null)
                return;

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].speed = speed;
            }
        }

        public static T? Field<T>(object? host, string fieldName) where T : class
        {
            if (host == null)
                return null;
            var field = host.GetType().GetField(fieldName, InstanceFlags);
            return field?.GetValue(host) as T;
        }

        public static string? FieldString(object? host, string fieldName)
        {
            if (host == null)
                return null;
            var field = host.GetType().GetField(fieldName, InstanceFlags);
            return field?.GetValue(host) as string;
        }

        public static bool PropertyBool(object? host, string propertyName)
        {
            if (host == null)
                return false;
            var property = host.GetType().GetProperty(propertyName, InstanceFlags);
            return property?.GetValue(host) is bool value && value;
        }

        public static int FieldInt(object? host, string fieldName)
        {
            if (host == null)
                return 0;
            var field = host.GetType().GetField(fieldName, InstanceFlags);
            return field?.GetValue(host) is int value ? value : 0;
        }

        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    }

    /// <summary>
    /// Rewrite a serialized duration from its first-seen original value.
    /// Later calls replace the field; they do not divide an already scaled value.
    /// </summary>
    internal static class DurationScale
    {
        private sealed class Originals
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
        }

        private static readonly ConditionalWeakTable<object, Originals> Table = new ConditionalWeakTable<object, Originals>();

        public static void Apply(object instance, string fieldName, float multiplier, bool instant = false)
        {
            if (multiplier < 1f)
                multiplier = 1f;

            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(float))
                return;

            var originals = Table.GetOrCreateValue(instance);
            if (!originals.Values.TryGetValue(fieldName, out var original))
            {
                original = (float)field.GetValue(instance);
                originals.Values[fieldName] = original;
            }

            var value = instant ? 0.0001f : original / multiplier;
            field.SetValue(instance, value);
        }
    }
}
